using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
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

        // Phase 0: Load all data upfront
        var seasonYear = season.StartDateUtc.Year;
        var apiRoundNames = (await footballDataService.GetRoundsForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
        var apiFixtures = (await footballDataService.GetAllFixturesForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
        var allRounds = await roundRepository.GetAllForSeasonAsync(season.Id, cancellationToken);
        var allApiTeamIds = apiFixtures.Where(f => f.Teams?.Home != null && f.Teams?.Away != null).SelectMany(f => new[] { f.Teams!.Home.Id, f.Teams!.Away.Id }).Distinct();
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

        // Phase 1: Filter valid fixtures
        var validFixtures = new List<ValidFixture>();

        foreach (var fixture in apiFixtures)
        {
            if (fixture.Fixture == null || fixture.Teams?.Home == null || fixture.Teams?.Away == null || fixture.League?.RoundName == null)
                continue;

            if (!teamsByApiId.TryGetValue(fixture.Teams.Home.Id, out var homeTeam) ||
                !teamsByApiId.TryGetValue(fixture.Teams.Away.Id, out var awayTeam))
                continue;

            validFixtures.Add(new ValidFixture(
                fixture.Fixture.Id,
                fixture.Fixture.Date.UtcDateTime,
                homeTeam.Id,
                awayTeam.Id,
                fixture.League.RoundName,
                fixture.Fixture.Status.Short));
        }

        // Phase 2: Calculate round date windows using gap-based boundaries
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

        var roundWindows = CalculateRoundWindows(roundSummaries);

        // Phase 3: Allocate each fixture to a round window
        var fixturesByRound = new Dictionary<string, List<ValidFixture>>();
        var unplaceableFixtures = new List<ValidFixture>();

        foreach (var fixture in validFixtures)
        {
            var targetWindow = roundWindows.FirstOrDefault(w => fixture.MatchDateTimeUtc >= w.WindowStart && fixture.MatchDateTimeUtc < w.WindowEnd);
            if (targetWindow != null)
            {
                if (!fixturesByRound.ContainsKey(targetWindow.ApiRoundName))
                    fixturesByRound[targetWindow.ApiRoundName] = [];

                fixturesByRound[targetWindow.ApiRoundName].Add(fixture);
            }
            else
            {
                unplaceableFixtures.Add(fixture);
            }
        }

        // Phase 4: Reconcile with existing database rounds
        var movedMatchesByTargetRound = new Dictionary<int, List<int>>();
        var allChangedRoundIds = new HashSet<int>();

        foreach (var window in roundWindows)
        {
            if (!fixturesByRound.TryGetValue(window.ApiRoundName, out var fixtures) || !fixtures.Any())
                continue;

            var round = allRounds.Values.FirstOrDefault(r => r.ApiRoundName == window.ApiRoundName);
            if (round == null)
            {
                var earliestMatchDateUtc = fixtures.Min(f => f.MatchDateTimeUtc);
                var newRound = Round.Create(
                    season.Id,
                    window.RoundNumber,
                    $"Gameweek {window.RoundNumber}",
                    earliestMatchDateUtc,
                    earliestMatchDateUtc.AddMinutes(-30),
                    window.ApiRoundName);

                round = await roundRepository.CreateAsync(newRound, cancellationToken);
                allRounds[round.Id] = round;
            }

            foreach (var fixture in fixtures)
            {
                if (matchesByExternalId.TryGetValue(fixture.ExternalId, out var existing))
                {
                    if (existing.Round.Id == round.Id)
                    {
                        if (existing.Match.MatchDateTimeUtc != fixture.MatchDateTimeUtc)
                        {
                            existing.Match.UpdateDate(fixture.MatchDateTimeUtc);
                            allChangedRoundIds.Add(round.Id);
                        }
                    }
                    else
                    {
                        existing.Round.RemoveMatch(existing.Match.Id);
                        allChangedRoundIds.Add(existing.Round.Id);

                        existing.Match.UpdateDate(fixture.MatchDateTimeUtc);
                        round.AcceptMatch(existing.Match);

                        if (!movedMatchesByTargetRound.ContainsKey(round.Id))
                            movedMatchesByTargetRound[round.Id] = [];

                        movedMatchesByTargetRound[round.Id].Add(existing.Match.Id);
                        allChangedRoundIds.Add(round.Id);

                        matchesByExternalId[fixture.ExternalId] = (round, existing.Match);
                    }
                }
                else
                {
                    round.AddMatch(fixture.HomeTeamId, fixture.AwayTeamId, fixture.MatchDateTimeUtc, fixture.ExternalId);
                    allChangedRoundIds.Add(round.Id);
                }
            }

            var activeMatches = round.Matches.Where(m => m.Status != MatchStatus.Postponed).ToList();
            if (!activeMatches.Any())
                continue;

            var roundEarliestMatchDateUtc = activeMatches.Min(m => m.MatchDateTimeUtc);
            if (roundEarliestMatchDateUtc == round.StartDateUtc)
                continue;

            round.UpdateDetails(
                round.RoundNumber,
                round.DisplayName,
                roundEarliestMatchDateUtc,
                roundEarliestMatchDateUtc.AddMinutes(-30),
                round.Status,
                round.ApiRoundName);
            allChangedRoundIds.Add(round.Id);
        }

        // Phase 4b: Update match statuses for postponed/rescheduled fixtures
        foreach (var fixture in validFixtures)
        {
            if (!matchesByExternalId.TryGetValue(fixture.ExternalId, out var existing))
                continue;

            if (fixture.ApiStatus == "PST" && existing.Match.Status is not (MatchStatus.Postponed or MatchStatus.Completed))
            {
                existing.Match.Postpone();
                allChangedRoundIds.Add(existing.Round.Id);
            }
            else if (fixture.ApiStatus != "PST" && existing.Match.Status == MatchStatus.Postponed)
            {
                existing.Match.Reschedule();
                allChangedRoundIds.Add(existing.Round.Id);
            }
        }

        // Phase 5: Delete stale matches
        var allApiExternalIds = new HashSet<int>(validFixtures.Select(f => f.ExternalId));
        var staleMatchIds = new List<int>();

        foreach (var round in allRounds.Values)
        {
            foreach (var match in round.Matches)
            {
                if (match.ExternalId.HasValue && !allApiExternalIds.Contains(match.ExternalId.Value))
                    staleMatchIds.Add(match.Id);
            }
        }

        if (staleMatchIds.Any())
        {
            var matchIdsWithPredictions = (await roundRepository.GetMatchIdsWithPredictionsAsync(staleMatchIds, cancellationToken)).ToHashSet();

            foreach (var round in allRounds.Values)
            {
                foreach (var match in round.Matches.ToList())
                {
                    if (!match.ExternalId.HasValue || allApiExternalIds.Contains(match.ExternalId.Value))
                        continue;

                    if (matchIdsWithPredictions.Contains(match.Id))
                    {
                        logger.LogWarning("Stale Match (ID: {MatchId}, ExternalId: {ExternalId}) has user predictions and cannot be deleted from Round (ID: {RoundId})", match.Id, match.ExternalId, round.Id);
                        continue;
                    }

                    round.RemoveMatch(match.Id);
                    allChangedRoundIds.Add(round.Id);
                }
            }
        }

        // Phase 6: Handle unplaceable fixtures
        foreach (var fixture in unplaceableFixtures)
        {
            if (matchesByExternalId.TryGetValue(fixture.ExternalId, out var existing))
            {
                if (existing.Match.MatchDateTimeUtc != fixture.MatchDateTimeUtc)
                {
                    existing.Match.UpdateDate(fixture.MatchDateTimeUtc);
                    allChangedRoundIds.Add(existing.Round.Id);
                }
            }

            logger.LogError("Match (ExternalId: {ExternalId}) could not be allocated to any round window. Match date (Value: {MatchDateTimeUtc})", fixture.ExternalId, fixture.MatchDateTimeUtc);
        }

        // Phase 7: Persist all changes
        // Move matches in the database first so their RoundId is updated before any
        // round's UpdateAsync runs. This prevents source rounds from deleting moved matches.
        foreach (var (targetRoundId, matchIds) in movedMatchesByTargetRound)
        {
            await roundRepository.MoveMatchesToRoundAsync(matchIds, targetRoundId, cancellationToken);
        }

        foreach (var roundId in allChangedRoundIds)
        {
            if (allRounds.TryGetValue(roundId, out var round))
                await roundRepository.UpdateAsync(round, cancellationToken);
        }

        // Phase 8: Publish/unpublish rounds based on updated start dates
        await mediator.Send(new PublishUpcomingRoundsCommand(), cancellationToken);
    }

    private async Task HandleTournamentSyncAsync(Season season, int apiLeagueId, CancellationToken cancellationToken)
    {
        var seasonYear = season.StartDateUtc.Year;

        // Load data
        var apiFixtures = (await footballDataService.GetAllFixturesForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
        var allRounds = await roundRepository.GetAllForSeasonAsync(season.Id, cancellationToken);
        var mappings = await tournamentRoundMappingRepository.GetBySeasonIdAsync(season.Id, cancellationToken);
        var allApiTeamIds = apiFixtures
            .Where(f => f.Teams?.Home != null && f.Teams?.Away != null)
            .SelectMany(f => new[] { f.Teams!.Home.Id, f.Teams!.Away.Id })
            .Distinct();
        var teamsByApiId = await teamRepository.GetByApiIdsAsync(allApiTeamIds, cancellationToken);

        // Build stage-to-mapping lookup
        var stageToMapping = new Dictionary<TournamentStage, TournamentRoundMapping>();
        foreach (var mapping in mappings)
        {
            foreach (var stage in mapping.GetStageList())
                stageToMapping[stage] = mapping;
        }

        // Filter valid fixtures (skip null teams — knockout teams not yet known)
        var validFixtures = new List<ValidFixture>();
        foreach (var fixture in apiFixtures)
        {
            if (fixture.Fixture == null || fixture.Teams?.Home == null || fixture.Teams?.Away == null || fixture.League?.RoundName == null)
                continue;

            if (!teamsByApiId.TryGetValue(fixture.Teams.Home.Id, out var homeTeam) ||
                !teamsByApiId.TryGetValue(fixture.Teams.Away.Id, out var awayTeam))
                continue;

            validFixtures.Add(new ValidFixture(
                fixture.Fixture.Id,
                fixture.Fixture.Date.UtcDateTime,
                homeTeam.Id,
                awayTeam.Id,
                fixture.League.RoundName,
                fixture.Fixture.Status.Short));
        }

        // Group fixtures by mapping
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
                logger.LogWarning("Tournament sync: stage (Value: {Stage}) has no mapping for Season (ID: {SeasonId})", stage, season.Id);
                continue;
            }

            if (!fixturesByMapping.ContainsKey(mapping.RoundNumber))
                fixturesByMapping[mapping.RoundNumber] = [];

            fixturesByMapping[mapping.RoundNumber].Add(fixture);
        }

        // Process each mapping
        var allChangedRoundIds = new HashSet<int>();

        foreach (var mapping in mappings)
        {
            if (!fixturesByMapping.TryGetValue(mapping.RoundNumber, out var fixtures) || !fixtures.Any())
                continue;

            // Find existing round
            var round = allRounds.Values.FirstOrDefault(r => r.RoundNumber == mapping.RoundNumber);
            if (round == null)
            {
                logger.LogWarning("Tournament sync: no round found for mapping RoundNumber (Value: {RoundNumber}) in Season (ID: {SeasonId})", mapping.RoundNumber, season.Id);
                continue;
            }

            foreach (var fixture in fixtures)
            {
                TournamentRoundNameParser.TryParseStage(fixture.ApiRoundName, out var fixtureStage);

                // Try to find an existing placeholder match to fill in
                var existingMatch = round.Matches.FirstOrDefault(m =>
                    m.ExternalId == fixture.ExternalId);

                if (existingMatch != null)
                {
                    // Already synced — update date if changed
                    if (existingMatch.MatchDateTimeUtc != fixture.MatchDateTimeUtc)
                    {
                        existingMatch.UpdateDate(fixture.MatchDateTimeUtc);
                        allChangedRoundIds.Add(round.Id);
                    }
                }
                else
                {
                    // Find an unassigned placeholder match for this stage
                    var stageDisplayName = TournamentRoundNameParser.GetDefaultDisplayName(fixtureStage);
                    var placeholder = round.Matches.FirstOrDefault(m =>
                        !m.AreTeamsConfirmed &&
                        m.ExternalId == null &&
                        m.ApiRoundName == stageDisplayName);

                    if (placeholder != null)
                    {
                        // Fill in the placeholder
                        placeholder.AssignTeams(fixture.HomeTeamId, fixture.AwayTeamId);
                        placeholder.UpdateDate(fixture.MatchDateTimeUtc);
                        placeholder.SetExternalId(fixture.ExternalId);
                        placeholder.SetApiRoundName(fixture.ApiRoundName);

                        allChangedRoundIds.Add(round.Id);
                    }
                    else
                    {
                        // No placeholder available — add as new match
                        round.AddMatch(fixture.HomeTeamId, fixture.AwayTeamId, fixture.MatchDateTimeUtc, fixture.ExternalId);
                        allChangedRoundIds.Add(round.Id);
                        logger.LogInformation("Tournament sync: added extra match (ExternalId: {ExternalId}) beyond expected count to Round (ID: {RoundId})", fixture.ExternalId, round.Id);
                    }
                }
            }

            // Update round dates based on actual matches
            var confirmedMatches = round.Matches
                .Where(m => m.AreTeamsConfirmed && m.Status != MatchStatus.Postponed)
                .ToList();

            if (confirmedMatches.Any())
            {
                var earliestMatchDateUtc = confirmedMatches.Min(m => m.MatchDateTimeUtc);
                if (earliestMatchDateUtc != round.StartDateUtc)
                {
                    round.UpdateDetails(
                        round.RoundNumber,
                        round.DisplayName,
                        earliestMatchDateUtc,
                        earliestMatchDateUtc.AddMinutes(-30),
                        round.Status,
                        round.ApiRoundName);
                    allChangedRoundIds.Add(round.Id);
                }
            }

            // Handle postponements
            foreach (var fixture in fixtures)
            {
                var match = round.Matches.FirstOrDefault(m => m.ExternalId == fixture.ExternalId);
                if (match == null)
                    continue;

                if (fixture.ApiStatus == "PST" && match.Status is not (MatchStatus.Postponed or MatchStatus.Completed))
                {
                    match.Postpone();
                    allChangedRoundIds.Add(round.Id);
                }
                else if (fixture.ApiStatus != "PST" && match.Status == MatchStatus.Postponed)
                {
                    match.Reschedule();
                    allChangedRoundIds.Add(round.Id);
                }
            }

            // Recompute per-batch custom lock times across the whole round. Doing this here (rather than only
            // when a placeholder is first filled) means every sync self-heals existing matches, so a schedule
            // change or a corrected batch is picked up without any manual intervention.
            if (round.RecalculateBatchPredictionLocks())
                allChangedRoundIds.Add(round.Id);
        }

        // Persist changes
        foreach (var roundId in allChangedRoundIds)
        {
            if (allRounds.TryGetValue(roundId, out var round))
                await roundRepository.UpdateAsync(round, cancellationToken);
        }

        // Publish/unpublish rounds
        await mediator.Send(new PublishUpcomingRoundsCommand(), cancellationToken);

        logger.LogInformation("Tournament sync completed for Season (ID: {SeasonId}). {FixtureCount} fixtures processed, {RoundCount} rounds updated", season.Id, validFixtures.Count, allChangedRoundIds.Count);
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
