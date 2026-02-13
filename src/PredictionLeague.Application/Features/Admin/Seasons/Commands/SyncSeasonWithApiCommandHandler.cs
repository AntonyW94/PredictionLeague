using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Guards;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class SyncSeasonWithApiCommandHandler : IRequestHandler<SyncSeasonWithApiCommand>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFootballDataService _footballDataService;
    private readonly IMediator _mediator;
    private readonly ILogger<SyncSeasonWithApiCommandHandler> _logger;

    public SyncSeasonWithApiCommandHandler(
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        IRoundRepository roundRepository,
        IFootballDataService footballDataService,
        IMediator mediator,
        ILogger<SyncSeasonWithApiCommandHandler> logger)
    {
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _roundRepository = roundRepository;
        _footballDataService = footballDataService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(SyncSeasonWithApiCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        if (season.ApiLeagueId == null)
            return;

        // Phase 0: Load all data upfront
        var seasonYear = season.StartDateUtc.Year;
        var apiRoundNames = (await _footballDataService.GetRoundsForSeasonAsync(season.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
        var apiFixtures = (await _footballDataService.GetAllFixturesForSeasonAsync(season.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
        var allRounds = await _roundRepository.GetAllForSeasonAsync(season.Id, cancellationToken);
        var allApiTeamIds = apiFixtures.Where(f => f.Teams?.Home != null && f.Teams?.Away != null).SelectMany(f => new[] { f.Teams!.Home.Id, f.Teams!.Away.Id }).Distinct();
        var teamsByApiId = await _teamRepository.GetByApiIdsAsync(allApiTeamIds, cancellationToken);
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
                fixture.League.RoundName));
        }

        // Phase 2: Calculate round date windows using gap-based boundaries
        var roundSummaries = new List<RoundFixtureSummary>();

        foreach (var apiRoundName in apiRoundNames)
        {
            if (!TryParseRoundNumber(apiRoundName, out var roundNumber))
                continue;

            var fixturesInApiRound = validFixtures
                .Where(f => f.ApiRoundName == apiRoundName)
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
                    earliestMatchDateUtc,
                    earliestMatchDateUtc.AddMinutes(-30),
                    window.ApiRoundName);

                round = await _roundRepository.CreateAsync(newRound, cancellationToken);
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

            if (round.Matches.Any())
            {
                var earliestMatchDateUtc = round.Matches.Min(m => m.MatchDateTimeUtc);
                if (earliestMatchDateUtc != round.StartDateUtc)
                {
                    round.UpdateDetails(
                        round.RoundNumber,
                        earliestMatchDateUtc,
                        earliestMatchDateUtc.AddMinutes(-30),
                        round.Status,
                        round.ApiRoundName);
                    allChangedRoundIds.Add(round.Id);
                }
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
            var matchIdsWithPredictions = (await _roundRepository.GetMatchIdsWithPredictionsAsync(staleMatchIds, cancellationToken)).ToHashSet();

            foreach (var round in allRounds.Values)
            {
                foreach (var match in round.Matches.ToList())
                {
                    if (!match.ExternalId.HasValue || allApiExternalIds.Contains(match.ExternalId.Value))
                        continue;

                    if (matchIdsWithPredictions.Contains(match.Id))
                    {
                        _logger.LogWarning("Stale Match (ID: {MatchId}, ExternalId: {ExternalId}) has user predictions and cannot be deleted from Round (ID: {RoundId})", match.Id, match.ExternalId, round.Id);
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

            _logger.LogError("Match (ExternalId: {ExternalId}) could not be allocated to any round window. Match date (Value: {MatchDateTimeUtc})", fixture.ExternalId, fixture.MatchDateTimeUtc);
        }

        // Phase 7: Persist all changes
        // Move matches in the database first so their RoundId is updated before any
        // round's UpdateAsync runs. This prevents source rounds from deleting moved matches.
        foreach (var (targetRoundId, matchIds) in movedMatchesByTargetRound)
        {
            await _roundRepository.MoveMatchesToRoundAsync(matchIds, targetRoundId, cancellationToken);
        }

        foreach (var roundId in allChangedRoundIds)
        {
            if (allRounds.TryGetValue(roundId, out var round))
                await _roundRepository.UpdateAsync(round, cancellationToken);
        }

        // Phase 8: Publish/unpublish rounds based on updated start dates
        await _mediator.Send(new PublishUpcomingRoundsCommand(), cancellationToken);
    }

    private static List<RoundWindow> CalculateRoundWindows(List<RoundFixtureSummary> sortedSummaries)
    {
        if (sortedSummaries.Count == 0)
            return [];

        if (sortedSummaries.Count == 1)
        {
            var only = sortedSummaries[0];
            return [new RoundWindow(only.ApiRoundName, only.RoundNumber, DateTime.MinValue, DateTime.MaxValue)];
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

    private record ValidFixture(int ExternalId, DateTime MatchDateTimeUtc, int HomeTeamId, int AwayTeamId, string ApiRoundName);

    private record RoundFixtureSummary(string ApiRoundName, int RoundNumber, DateTime MedianDateUtc);

    private record RoundWindow(string ApiRoundName, int RoundNumber, DateTime WindowStart, DateTime WindowEnd);
}
