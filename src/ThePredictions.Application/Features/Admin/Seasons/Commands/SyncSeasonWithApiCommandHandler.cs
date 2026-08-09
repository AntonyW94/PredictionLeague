using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public class SyncSeasonWithApiCommandHandler(
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    ITeamRepository teamRepository,
    IRoundRepository roundRepository,
    ITournamentRoundMappingRepository tournamentRoundMappingRepository,
    IFootballDataService footballDataService,
    IMediator mediator,
    ILogger<SyncSeasonWithApiCommandHandler> logger) : IRequestHandler<SyncSeasonWithApiCommand>
{
    public async Task Handle(SyncSeasonWithApiCommand request, CancellationToken cancellationToken)
    {
        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        var competition = await competitionRepository.GetByIdAsync(season.CompetitionId, cancellationToken);
        Guard.Against.EntityNotFound(season.CompetitionId, competition, "Competition");

        if (competition.ApiLeagueId == null)
            return;

        var apiLeagueId = competition.ApiLeagueId.Value;

        if (competition.IsTournament)
        {
            await HandleTournamentSyncAsync(season, apiLeagueId, cancellationToken);
            return;
        }

        var context = await LoadLeagueSyncContextAsync(season, apiLeagueId, cancellationToken);

        AllocateFixturesToWindows(context);
        await ReconcileRoundsAsync(context, cancellationToken);
        ApplyStatusChanges(context);
        await RemoveStaleMatchesAsync(context, cancellationToken);
        RecordUnplaceableFixtures(context);
        await PersistChangesAsync(context, cancellationToken);

        // Publish/unpublish rounds based on the updated start dates.
        await mediator.Send(new PublishUpcomingRoundsCommand(), cancellationToken);
    }

    /// <summary>
    /// Everything one league sync works on. Built once up front so each phase below reads and
    /// mutates the same view rather than re-querying.
    /// </summary>
    private sealed class LeagueSyncContext
    {
        public required Season Season { get; init; }
        public required Dictionary<int, Round> AllRounds { get; init; }
        public required Dictionary<int, (Round Round, Match Match)> MatchesByExternalId { get; init; }
        public required List<ValidFixture> ValidFixtures { get; init; }
        public required List<RoundWindow> RoundWindows { get; init; }

        public Dictionary<string, List<ValidFixture>> FixturesByRound { get; } = [];
        public List<ValidFixture> UnplaceableFixtures { get; } = [];
        public Dictionary<int, List<int>> MovedMatchesByTargetRound { get; } = [];
        public HashSet<int> ChangedRoundIds { get; } = [];
    }

    /// <summary>Loads the feed and the existing rounds, and works out each round's date window.</summary>
    private async Task<LeagueSyncContext> LoadLeagueSyncContextAsync(Season season, int apiLeagueId, CancellationToken cancellationToken)
    {
        var seasonYear = season.StartDateUtc.Year;
        var apiRoundNames = (await footballDataService.GetRoundsForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
        var apiFixtures = (await footballDataService.GetAllFixturesForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
        var allRounds = await roundRepository.GetAllForSeasonAsync(season.Id, cancellationToken);
        // f.Teams! on the second check: reaching it means Home was non-null, so Teams cannot be
        // null here and a second null-conditional would add a branch that never fires.
        var allApiTeamIds = apiFixtures.Where(f => f.Teams?.Home != null && f.Teams!.Away != null).SelectMany(f => new[] { f.Teams!.Home.Id, f.Teams!.Away.Id }).Distinct();
        var teamsByApiId = await teamRepository.GetByApiIdsAsync(allApiTeamIds, cancellationToken);

        var matchesByExternalId = new Dictionary<int, (Round Round, Match Match)>();

        foreach (var round in allRounds.Values)
        {
            foreach (var match in round.Matches)
            {
                if (match.ExternalId.HasValue)
                    matchesByExternalId[match.ExternalId.Value] = (round, match);
            }
        }

        var validFixtures = BuildValidFixtures(apiFixtures, teamsByApiId);

        return new LeagueSyncContext
        {
            Season = season,
            AllRounds = allRounds,
            MatchesByExternalId = matchesByExternalId,
            ValidFixtures = validFixtures,
            RoundWindows = CalculateRoundWindows(BuildRoundSummaries(apiRoundNames, validFixtures))
        };
    }

    /// <summary>Every fixture the feed described fully enough to act on, with teams resolved locally.</summary>
    private static List<ValidFixture> BuildValidFixtures(List<FixtureResponse> apiFixtures, IReadOnlyDictionary<int, Team> teamsByApiId)
    {
        var validFixtures = new List<ValidFixture>();

        foreach (var fixture in apiFixtures)
        {
            if (TryBuildValidFixture(fixture, teamsByApiId, out var validFixture))
                validFixtures.Add(validFixture);
        }

        return validFixtures;
    }

    /// <summary>
    /// False when the feed left something out - no kick-off detail, only one side of the tie named,
    /// no stage - or when either team is one the site does not hold.
    /// </summary>
    private static bool TryBuildValidFixture(FixtureResponse fixture, IReadOnlyDictionary<int, Team> teamsByApiId, out ValidFixture validFixture)
    {
        validFixture = null!;

        if (!IsFullyDescribed(fixture))
            return false;

        if (!teamsByApiId.TryGetValue(fixture.Teams!.Home.Id, out var homeTeam) ||
            !teamsByApiId.TryGetValue(fixture.Teams.Away.Id, out var awayTeam))
            return false;

        validFixture = new ValidFixture(
            fixture.Fixture!.Id,
            fixture.Fixture.Date.UtcDateTime,
            homeTeam.Id,
            awayTeam.Id,
            fixture.League!.RoundName!,
            fixture.Fixture.Status.Short);

        return true;
    }

    /// <summary>
    /// Whether the feed gave enough to act on: kick-off detail, both sides of the tie named, and a
    /// stage. fixture.Teams! on the third check - getting past the second proves Teams is not null.
    /// </summary>
    private static bool IsFullyDescribed(FixtureResponse fixture) =>
        fixture.Fixture != null
        && fixture.Teams?.Home != null
        && fixture.Teams!.Away != null
        && fixture.League?.RoundName != null;

    /// <summary>
    /// One summary per numbered API round, carrying the median date of its fixtures. Ordered by that
    /// median, then by round number so two rounds sharing one keep a stable order between syncs.
    /// </summary>
    private static List<RoundFixtureSummary> BuildRoundSummaries(List<string> apiRoundNames, List<ValidFixture> validFixtures)
    {
        var roundSummaries = new List<RoundFixtureSummary>();

        foreach (var apiRoundName in apiRoundNames)
        {
            if (!TryParseRoundNumber(apiRoundName, out var roundNumber))
                continue;

            var fixturesInApiRound = validFixtures
                .Where(f => f.ApiRoundName == apiRoundName && f.ApiStatus != "PST")
                .OrderBy(f => f.MatchDateTimeUtc)
                .ToList();

            if (!fixturesInApiRound.Any())
                continue;

            var medianDateUtc = fixturesInApiRound[fixturesInApiRound.Count / 2].MatchDateTimeUtc;
            roundSummaries.Add(new RoundFixtureSummary(apiRoundName, roundNumber, medianDateUtc));
        }

        roundSummaries.Sort((a, b) =>
        {
            var cmp = a.MedianDateUtc.CompareTo(b.MedianDateUtc);
            return cmp != 0 ? cmp : a.RoundNumber.CompareTo(b.RoundNumber);
        });

        return roundSummaries;
    }

    /// <summary>Puts each fixture in the round window its kick-off falls inside.</summary>
    private static void AllocateFixturesToWindows(LeagueSyncContext context)
    {
        foreach (var fixture in context.ValidFixtures)
        {
            // Windows are ordered and contiguous from DateTime.MinValue, so the first one whose end
            // is after the fixture is the one it belongs to - no start check needed (it could never
            // be false). With no windows at all, the fixture is unplaceable.
            var targetWindow = context.RoundWindows.FirstOrDefault(w => fixture.MatchDateTimeUtc < w.WindowEnd);

            if (targetWindow == null)
            {
                context.UnplaceableFixtures.Add(fixture);
                continue;
            }

            if (!context.FixturesByRound.ContainsKey(targetWindow.ApiRoundName))
                context.FixturesByRound[targetWindow.ApiRoundName] = [];

            context.FixturesByRound[targetWindow.ApiRoundName].Add(fixture);
        }
    }

    /// <summary>Creates any missing round, files each fixture into it, then realigns its start date.</summary>
    private async Task ReconcileRoundsAsync(LeagueSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var window in context.RoundWindows)
        {
            // Two rounds sharing a median produce a zero-width window, which takes no fixtures at
            // all. No emptiness check beyond this: the dictionary only ever receives non-empty lists.
            if (!context.FixturesByRound.TryGetValue(window.ApiRoundName, out var fixtures))
                continue;

            var round = context.AllRounds.Values.FirstOrDefault(r => r.ApiRoundName == window.ApiRoundName)
                        ?? await CreateRoundForWindowAsync(context, window, fixtures, cancellationToken);

            foreach (var fixture in fixtures)
            {
                ReconcileFixture(context, round, fixture);
            }

            RealignRoundStart(context, round);
        }
    }

    private async Task<Round> CreateRoundForWindowAsync(
        LeagueSyncContext context, RoundWindow window, List<ValidFixture> fixtures, CancellationToken cancellationToken)
    {
        var earliestMatchDateUtc = fixtures.Min(f => f.MatchDateTimeUtc);
        var newRound = Round.Create(
            context.Season.Id,
            window.RoundNumber,
            $"Gameweek {window.RoundNumber}",
            earliestMatchDateUtc,
            earliestMatchDateUtc.AddMinutes(-30),
            window.ApiRoundName);

        var created = await roundRepository.CreateAsync(newRound, cancellationToken);
        context.AllRounds[created.Id] = created;
        return created;
    }

    /// <summary>Adds the fixture, moves it in from another round, or just corrects its kick-off.</summary>
    private static void ReconcileFixture(LeagueSyncContext context, Round round, ValidFixture fixture)
    {
        if (!context.MatchesByExternalId.TryGetValue(fixture.ExternalId, out var existing))
        {
            round.AddMatch(fixture.HomeTeamId, fixture.AwayTeamId, fixture.MatchDateTimeUtc, fixture.ExternalId);
            context.ChangedRoundIds.Add(round.Id);
            return;
        }

        if (existing.Round.Id == round.Id)
        {
            if (existing.Match.MatchDateTimeUtc != fixture.MatchDateTimeUtc)
            {
                existing.Match.UpdateDate(fixture.MatchDateTimeUtc);
                context.ChangedRoundIds.Add(round.Id);
            }

            return;
        }

        existing.Round.RemoveMatch(existing.Match.Id);
        context.ChangedRoundIds.Add(existing.Round.Id);

        existing.Match.UpdateDate(fixture.MatchDateTimeUtc);
        round.AcceptMatch(existing.Match);

        if (!context.MovedMatchesByTargetRound.ContainsKey(round.Id))
            context.MovedMatchesByTargetRound[round.Id] = [];

        context.MovedMatchesByTargetRound[round.Id].Add(existing.Match.Id);
        context.ChangedRoundIds.Add(round.Id);

        context.MatchesByExternalId[fixture.ExternalId] = (round, existing.Match);
    }

    /// <summary>Pulls the round's start back to its earliest fixture that is still going ahead.</summary>
    private static void RealignRoundStart(LeagueSyncContext context, Round round)
    {
        var activeMatches = round.Matches.Where(m => m.Status != MatchStatus.Postponed).ToList();
        if (!activeMatches.Any())
            return;

        var roundEarliestMatchDateUtc = activeMatches.Min(m => m.MatchDateTimeUtc);
        if (roundEarliestMatchDateUtc == round.StartDateUtc)
            return;

        round.UpdateDetails(
            round.RoundNumber,
            round.DisplayName,
            roundEarliestMatchDateUtc,
            roundEarliestMatchDateUtc.AddMinutes(-30),
            round.Status,
            round.ApiRoundName);
        context.ChangedRoundIds.Add(round.Id);
    }

    /// <summary>Postpones matches the feed has called off, and reinstates any that are back on.</summary>
    private static void ApplyStatusChanges(LeagueSyncContext context)
    {
        foreach (var fixture in context.ValidFixtures)
        {
            if (!context.MatchesByExternalId.TryGetValue(fixture.ExternalId, out var existing))
                continue;

            if (fixture.ApiStatus == "PST" && existing.Match.Status is not (MatchStatus.Postponed or MatchStatus.Completed))
            {
                existing.Match.Postpone();
                context.ChangedRoundIds.Add(existing.Round.Id);
            }
            else if (fixture.ApiStatus != "PST" && existing.Match.Status == MatchStatus.Postponed)
            {
                existing.Match.Reschedule();
                context.ChangedRoundIds.Add(existing.Round.Id);
            }
        }
    }

    /// <summary>
    /// Drops matches the feed no longer lists. A match players have already predicted is kept and
    /// logged instead, since deleting it would destroy their predictions.
    /// </summary>
    private async Task RemoveStaleMatchesAsync(LeagueSyncContext context, CancellationToken cancellationToken)
    {
        var isStale = StaleMatchPredicate(context);
        var staleMatchIds = context.AllRounds.Values
            .SelectMany(r => r.Matches)
            .Where(isStale)
            .Select(m => m.Id)
            .ToList();

        if (!staleMatchIds.Any())
            return;

        var matchIdsWithPredictions = (await roundRepository.GetMatchIdsWithPredictionsAsync(staleMatchIds, cancellationToken)).ToHashSet();

        foreach (var round in context.AllRounds.Values)
        {
            foreach (var match in round.Matches.ToList().Where(isStale))
            {
                if (matchIdsWithPredictions.Contains(match.Id))
                {
                    logger.LogWarning("Stale Match (ID: {MatchId}, ExternalId: {ExternalId}) has user predictions and cannot be deleted from Round (ID: {RoundId})", match.Id, match.ExternalId, round.Id);
                    continue;
                }

                round.RemoveMatch(match.Id);
                context.ChangedRoundIds.Add(round.Id);
            }
        }
    }

    /// <summary>
    /// Stale means the feed once listed the match and no longer does. A match added by hand carries
    /// no external id and so is never stale.
    /// </summary>
    private static Func<Match, bool> StaleMatchPredicate(LeagueSyncContext context)
    {
        var allApiExternalIds = new HashSet<int>(context.ValidFixtures.Select(f => f.ExternalId));
        return match => match.ExternalId.HasValue && !allApiExternalIds.Contains(match.ExternalId.Value);
    }

    /// <summary>Logs fixtures that fit no window, still recording any kick-off change they carry.</summary>
    private void RecordUnplaceableFixtures(LeagueSyncContext context)
    {
        foreach (var fixture in context.UnplaceableFixtures)
        {
            if (context.MatchesByExternalId.TryGetValue(fixture.ExternalId, out var existing)
                && existing.Match.MatchDateTimeUtc != fixture.MatchDateTimeUtc)
            {
                existing.Match.UpdateDate(fixture.MatchDateTimeUtc);
                context.ChangedRoundIds.Add(existing.Round.Id);
            }

            logger.LogError("Match (ExternalId: {ExternalId}) could not be allocated to any round window. Match date (Value: {MatchDateTimeUtc})", fixture.ExternalId, fixture.MatchDateTimeUtc);
        }
    }

    /// <summary>
    /// Moves matches in the database first so their RoundId is updated before any round's
    /// UpdateAsync runs - otherwise a source round would delete the match it just gave away.
    /// </summary>
    private async Task PersistChangesAsync(LeagueSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var (targetRoundId, matchIds) in context.MovedMatchesByTargetRound)
        {
            await roundRepository.MoveMatchesToRoundAsync(matchIds, targetRoundId, cancellationToken);
        }

        foreach (var roundId in context.ChangedRoundIds)
        {
            if (context.AllRounds.TryGetValue(roundId, out var round))
                await roundRepository.UpdateAsync(round, cancellationToken);
        }
    }

    private async Task HandleTournamentSyncAsync(Season season, int apiLeagueId, CancellationToken cancellationToken)
    {
        var seasonYear = season.StartDateUtc.Year;

        var apiFixtures = (await footballDataService.GetAllFixturesForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
        var allRounds = await roundRepository.GetAllForSeasonAsync(season.Id, cancellationToken);
        var mappings = await tournamentRoundMappingRepository.GetBySeasonIdAsync(season.Id, cancellationToken);
        var allApiTeamIds = apiFixtures
            // f.Teams! as above: Home being non-null already proves Teams is not null.
            .Where(f => f.Teams?.Home != null && f.Teams!.Away != null)
            .SelectMany(f => new[] { f.Teams!.Home.Id, f.Teams!.Away.Id })
            .Distinct();
        var teamsByApiId = await teamRepository.GetByApiIdsAsync(allApiTeamIds, cancellationToken);

        var validFixtures = BuildValidFixtures(apiFixtures, teamsByApiId);
        var fixturesByMapping = GroupFixturesByMapping(validFixtures, mappings, season.Id);
        var changedRoundIds = new HashSet<int>();

        foreach (var mapping in mappings)
        {
            if (!fixturesByMapping.TryGetValue(mapping.RoundNumber, out var fixtures) || !fixtures.Any())
                continue;

            var round = allRounds.Values.FirstOrDefault(r => r.RoundNumber == mapping.RoundNumber);
            if (round == null)
            {
                logger.LogWarning("Tournament sync: no round found for mapping RoundNumber (Value: {RoundNumber}) in Season (ID: {SeasonId})", mapping.RoundNumber, season.Id);
                continue;
            }

            SyncTournamentRound(round, fixtures, changedRoundIds);
        }

        foreach (var roundId in changedRoundIds)
        {
            if (allRounds.TryGetValue(roundId, out var round))
                await roundRepository.UpdateAsync(round, cancellationToken);
        }

        await mediator.Send(new PublishUpcomingRoundsCommand(), cancellationToken);

        logger.LogInformation("Tournament sync completed for Season (ID: {SeasonId}). {FixtureCount} fixtures processed, {RoundCount} rounds updated", season.Id, validFixtures.Count, changedRoundIds.Count);
    }

    /// <summary>
    /// Buckets each fixture under the round its stage is mapped to. A stage the feed names but the
    /// season has no mapping for is logged and skipped rather than guessed at.
    /// </summary>
    private Dictionary<int, List<ValidFixture>> GroupFixturesByMapping(
        List<ValidFixture> validFixtures, IReadOnlyList<TournamentRoundMapping> mappings, int seasonId)
    {
        var stageToMapping = new Dictionary<TournamentStage, TournamentRoundMapping>();

        foreach (var mapping in mappings)
        {
            foreach (var stage in mapping.GetStageList())
                stageToMapping[stage] = mapping;
        }

        var fixturesByMapping = new Dictionary<int, List<ValidFixture>>();

        foreach (var fixture in validFixtures)
        {
            if (!TournamentRoundNameParser.TryParseStage(fixture.ApiRoundName, out var stage))
            {
                logger.LogWarning("Tournament sync: unrecognised API round name (Value: {ApiRoundName})", fixture.ApiRoundName);
                continue;
            }

            if (!stageToMapping.TryGetValue(stage, out var mapping))
            {
                logger.LogWarning("Tournament sync: stage (Value: {Stage}) has no mapping for Season (ID: {SeasonId})", stage, seasonId);
                continue;
            }

            if (!fixturesByMapping.ContainsKey(mapping.RoundNumber))
                fixturesByMapping[mapping.RoundNumber] = [];

            fixturesByMapping[mapping.RoundNumber].Add(fixture);
        }

        return fixturesByMapping;
    }

    /// <summary>Files every tie into the round, then realigns its start, statuses and lock times.</summary>
    private void SyncTournamentRound(Round round, List<ValidFixture> fixtures, HashSet<int> changedRoundIds)
    {
        foreach (var fixture in fixtures)
        {
            SyncTournamentFixture(round, fixture, changedRoundIds);
        }

        RealignTournamentRoundStart(round, changedRoundIds);
        ApplyTournamentStatusChanges(round, fixtures, changedRoundIds);

        // Recompute per-batch custom lock times across the whole round. Doing this here (rather than only
        // when a placeholder is first filled) means every sync self-heals existing matches, so a schedule
        // change or a corrected batch is picked up without any manual intervention.
        if (round.RecalculateBatchPredictionLocks())
            changedRoundIds.Add(round.Id);
    }

    /// <summary>
    /// Corrects a tie already synced, otherwise fills the first free placeholder for its stage, and
    /// failing that adds it outright.
    /// </summary>
    private void SyncTournamentFixture(Round round, ValidFixture fixture, HashSet<int> changedRoundIds)
    {
        var existingMatch = round.Matches.FirstOrDefault(m => m.ExternalId == fixture.ExternalId);

        if (existingMatch != null)
        {
            if (existingMatch.MatchDateTimeUtc != fixture.MatchDateTimeUtc)
            {
                existingMatch.UpdateDate(fixture.MatchDateTimeUtc);
                changedRoundIds.Add(round.Id);
            }

            return;
        }

        TournamentRoundNameParser.TryParseStage(fixture.ApiRoundName, out var fixtureStage);
        var stageDisplayName = TournamentRoundNameParser.GetDefaultDisplayName(fixtureStage);
        var placeholder = round.Matches.FirstOrDefault(m =>
            !m.AreTeamsConfirmed &&
            m.ExternalId == null &&
            m.ApiRoundName == stageDisplayName);

        if (placeholder == null)
        {
            round.AddMatch(fixture.HomeTeamId, fixture.AwayTeamId, fixture.MatchDateTimeUtc, fixture.ExternalId);
            changedRoundIds.Add(round.Id);
            logger.LogInformation("Tournament sync: added extra match (ExternalId: {ExternalId}) beyond expected count to Round (ID: {RoundId})", fixture.ExternalId, round.Id);
            return;
        }

        placeholder.AssignTeams(fixture.HomeTeamId, fixture.AwayTeamId);
        placeholder.UpdateDate(fixture.MatchDateTimeUtc);
        placeholder.SetExternalId(fixture.ExternalId);
        placeholder.SetApiRoundName(fixture.ApiRoundName);

        changedRoundIds.Add(round.Id);
    }

    /// <summary>Pulls the round's start back to its earliest tie that has both sides confirmed.</summary>
    private static void RealignTournamentRoundStart(Round round, HashSet<int> changedRoundIds)
    {
        var confirmedMatches = round.Matches
            .Where(m => m.AreTeamsConfirmed && m.Status != MatchStatus.Postponed)
            .ToList();

        if (!confirmedMatches.Any())
            return;

        var earliestMatchDateUtc = confirmedMatches.Min(m => m.MatchDateTimeUtc);
        if (earliestMatchDateUtc == round.StartDateUtc)
            return;

        round.UpdateDetails(
            round.RoundNumber,
            round.DisplayName,
            earliestMatchDateUtc,
            earliestMatchDateUtc.AddMinutes(-30),
            round.Status,
            round.ApiRoundName);
        changedRoundIds.Add(round.Id);
    }

    /// <summary>Postpones ties the feed has called off, and reinstates any that are back on.</summary>
    private static void ApplyTournamentStatusChanges(Round round, List<ValidFixture> fixtures, HashSet<int> changedRoundIds)
    {
        foreach (var fixture in fixtures)
        {
            // Always present: the sync above either matched this fixture to an existing match,
            // stamped its id onto a placeholder, or added it - so there is no null case here.
            var match = round.Matches.First(m => m.ExternalId == fixture.ExternalId);

            if (fixture.ApiStatus == "PST" && match.Status is not (MatchStatus.Postponed or MatchStatus.Completed))
            {
                match.Postpone();
                changedRoundIds.Add(round.Id);
            }
            else if (fixture.ApiStatus != "PST" && match.Status == MatchStatus.Postponed)
            {
                match.Reschedule();
                changedRoundIds.Add(round.Id);
            }
        }
    }

    // internal so the window maths can be unit tested directly; InternalsVisibleTo already
    // exposes this assembly to ThePredictions.Application.Tests.Unit.
    internal static List<RoundWindow> CalculateRoundWindows(List<RoundFixtureSummary> sortedSummaries)
    {
        switch (sortedSummaries.Count)
        {
            case 0:
                return [];
            case 1:
            {
                var only = sortedSummaries[0];
                return [new RoundWindow(only.ApiRoundName, only.RoundNumber, DateTime.MinValue, DateTime.MaxValue)];
            }
        }

        // Calculate boundaries as midpoints between consecutive round medians.
        // A fixture closer to one round's median than the next will naturally
        // fall into the nearer round's window.
        var boundaries = new DateTime[sortedSummaries.Count - 1];
        for (var i = 0; i < boundaries.Length; i++)
        {
            var currentMedian = sortedSummaries[i].MedianDateUtc;
            var nextMedian = sortedSummaries[i + 1].MedianDateUtc;
            var midpointTicks = currentMedian.Ticks + (nextMedian.Ticks - currentMedian.Ticks) / 2;
            boundaries[i] = new DateTime(midpointTicks, DateTimeKind.Utc);
        }

        // Build windows from boundaries
        var windows = new List<RoundWindow>(sortedSummaries.Count);
        for (var i = 0; i < sortedSummaries.Count; i++)
        {
            var summary = sortedSummaries[i];
            var windowStart = i == 0 ? DateTime.MinValue : boundaries[i - 1];
            var windowEnd = i == sortedSummaries.Count - 1 ? DateTime.MaxValue : boundaries[i];
            windows.Add(new RoundWindow(summary.ApiRoundName, summary.RoundNumber, windowStart, windowEnd));
        }

        return windows;
    }

    private static bool TryParseRoundNumber(string apiRoundName, out int roundNumber)
    {
        roundNumber = 0;
        var parts = apiRoundName.Split(" - ");
        return parts.Length > 1 && int.TryParse(parts[^1], out roundNumber);
    }

    private record ValidFixture(int ExternalId, DateTime MatchDateTimeUtc, int HomeTeamId, int AwayTeamId, string ApiRoundName, string ApiStatus);

    internal record RoundFixtureSummary(string ApiRoundName, int RoundNumber, DateTime MedianDateUtc);

    internal record RoundWindow(string ApiRoundName, int RoundNumber, DateTime WindowStart, DateTime WindowEnd);
}
