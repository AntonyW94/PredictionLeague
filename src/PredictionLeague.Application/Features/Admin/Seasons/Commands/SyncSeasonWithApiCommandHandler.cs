using Ardalis.GuardClauses;
using MediatR;
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

    public SyncSeasonWithApiCommandHandler(
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        IRoundRepository roundRepository,
        IFootballDataService footballDataService)
    {
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _roundRepository = roundRepository;
        _footballDataService = footballDataService;
    }

    public async Task Handle(SyncSeasonWithApiCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        if (season.ApiLeagueId == null)
            return;

        var seasonYear = season.StartDateUtc.Year;
        var apiRoundNames = (await _footballDataService.GetRoundsForSeasonAsync(season.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();

        var currentSeasonFixtures = (await _footballDataService.GetAllFixturesForSeasonAsync(season.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();

        var allSeasonRounds = await _roundRepository.GetAllForSeasonAsync(season.Id, cancellationToken);

        var matchesByExternalId = new Dictionary<int, (Round Round, Match Match)>();
        foreach (var round in allSeasonRounds.Values)
        {
            foreach (var match in round.Matches)
            {
                if (match.ExternalId.HasValue)
                    matchesByExternalId[match.ExternalId.Value] = (round, match);
            }
        }

        var rescheduledMatchesToCheck = new List<FootballApi.DTOs.FixtureResponse>();
        var roundsWithRemovedMatches = new HashSet<int>();

        foreach (var apiRoundName in apiRoundNames)
        {
            var hasChanges = false;
            var currentRoundFixtures = currentSeasonFixtures.Where(f => f.League?.RoundName == apiRoundName).ToList();

            var round = allSeasonRounds.Values.FirstOrDefault(r => r.ApiRoundName == apiRoundName);
            if (round == null)
            {
                if (!currentRoundFixtures.Any())
                    continue;

                var earliestMatchDate = currentRoundFixtures.Min(f => f.Fixture?.Date);
                if (earliestMatchDate == null)
                    continue;

                var deadlineUtc = earliestMatchDate.Value.UtcDateTime.AddMinutes(-30);

                if (int.TryParse(apiRoundName.Split(" - ").LastOrDefault(), out var roundNumber))
                {
                    var newRound = Round.Create(
                        season.Id,
                        roundNumber,
                        earliestMatchDate.Value.UtcDateTime,
                        deadlineUtc,
                        apiRoundName
                    );

                    round = await _roundRepository.CreateAsync(newRound, cancellationToken);
                    allSeasonRounds[round.Id] = round;
                    hasChanges = true;
                }
                else
                {
                    continue;
                }
            }

            var matchesPlacedFromList = new List<FootballApi.DTOs.FixtureResponse>();

            foreach (var rescheduledMatch in rescheduledMatchesToCheck)
            {
                if (rescheduledMatch.Fixture == null || rescheduledMatch.Teams?.Home == null || rescheduledMatch.Teams?.Away == null)
                    continue;

                var fixtureDateUtc = rescheduledMatch.Fixture.Date.UtcDateTime;

                if (!IsMatchInRoundTimespan(fixtureDateUtc, round))
                    continue;

                if (matchesByExternalId.TryGetValue(rescheduledMatch.Fixture.Id, out var existing) && existing.Round.Id != round.Id)
                {
                    existing.Round.RemoveMatch(existing.Match.Id);
                    roundsWithRemovedMatches.Add(existing.Round.Id);

                    existing.Match.UpdateDate(fixtureDateUtc);
                    round.AcceptMatch(existing.Match);

                    matchesByExternalId[rescheduledMatch.Fixture.Id] = (round, existing.Match);
                    hasChanges = true;
                }
                else if (!matchesByExternalId.ContainsKey(rescheduledMatch.Fixture.Id))
                {
                    var homeTeam = await _teamRepository.GetByApiIdAsync(rescheduledMatch.Teams.Home.Id, cancellationToken);
                    var awayTeam = await _teamRepository.GetByApiIdAsync(rescheduledMatch.Teams.Away.Id, cancellationToken);

                    if (homeTeam != null && awayTeam != null)
                    {
                        if (!round.Matches.Any(m => m.HomeTeamId == homeTeam.Id && m.AwayTeamId == awayTeam.Id))
                        {
                            round.AddMatch(homeTeam.Id, awayTeam.Id, fixtureDateUtc, rescheduledMatch.Fixture.Id);
                            hasChanges = true;
                        }
                    }
                }

                matchesPlacedFromList.Add(rescheduledMatch);
            }

            rescheduledMatchesToCheck.RemoveAll(m => matchesPlacedFromList.Contains(m));

            var existingMatchesInRound = round.Matches.ToDictionary(m => m.ExternalId ?? 0);

            foreach (var fixture in currentRoundFixtures)
            {
                if (fixture.Fixture == null || fixture.Teams?.Home == null || fixture.Teams?.Away == null)
                    continue;

                var fixtureDateUtc = fixture.Fixture.Date.UtcDateTime;

                if (existingMatchesInRound.TryGetValue(fixture.Fixture.Id, out var localMatch))
                {
                    if (localMatch.MatchDateTimeUtc == fixtureDateUtc)
                        continue;

                    localMatch.UpdateDate(fixtureDateUtc);
                    hasChanges = true;
                }
                else if (matchesByExternalId.TryGetValue(fixture.Fixture.Id, out var existingInOtherRound) && existingInOtherRound.Round.Id != round.Id)
                {
                    existingInOtherRound.Round.RemoveMatch(existingInOtherRound.Match.Id);
                    roundsWithRemovedMatches.Add(existingInOtherRound.Round.Id);

                    existingInOtherRound.Match.UpdateDate(fixtureDateUtc);
                    round.AcceptMatch(existingInOtherRound.Match);

                    matchesByExternalId[fixture.Fixture.Id] = (round, existingInOtherRound.Match);
                    hasChanges = true;
                }
                else
                {
                    if (IsMatchInRoundTimespan(fixtureDateUtc, round))
                    {
                        var homeTeam = await _teamRepository.GetByApiIdAsync(fixture.Teams.Home.Id, cancellationToken);
                        var awayTeam = await _teamRepository.GetByApiIdAsync(fixture.Teams.Away.Id, cancellationToken);

                        if (homeTeam == null || awayTeam == null)
                            continue;

                        round.AddMatch(homeTeam.Id, awayTeam.Id, fixtureDateUtc, fixture.Fixture.Id);
                        hasChanges = true;
                    }
                    else
                    {
                        rescheduledMatchesToCheck.Add(fixture);
                    }
                }
            }

            if (round.Matches.Any())
            {
                var earliestMatchDateUtc = round.Matches.Min(m => m.MatchDateTimeUtc);
                if (earliestMatchDateUtc != round.StartDateUtc)
                {
                    round.UpdateDetails(round.RoundNumber, earliestMatchDateUtc, earliestMatchDateUtc.AddMinutes(-30), round.Status, round.ApiRoundName);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await _roundRepository.UpdateAsync(round, cancellationToken);
                roundsWithRemovedMatches.Remove(round.Id);
            }
        }

        foreach (var roundId in roundsWithRemovedMatches)
        {
            if (!allSeasonRounds.TryGetValue(roundId, out var round))
                continue;

            if (round.Matches.Any())
            {
                var earliestMatchDateUtc = round.Matches.Min(m => m.MatchDateTimeUtc);
                if (earliestMatchDateUtc != round.StartDateUtc)
                    round.UpdateDetails(round.RoundNumber, earliestMatchDateUtc, earliestMatchDateUtc.AddMinutes(-30), round.Status, round.ApiRoundName);
            }

            await _roundRepository.UpdateAsync(round, cancellationToken);
        }
    }

    private static bool IsMatchInRoundTimespan(DateTime apiFixtureDate, Round round)
    {
        const DayOfWeek targetStartDay = DayOfWeek.Wednesday;

        var daysToSubtract = ((int)round.StartDateUtc.DayOfWeek - (int)targetStartDay + 7) % 7;
        var roundStartDate = round.StartDateUtc.AddDays(-daysToSubtract).Date;
        var roundEndDate = roundStartDate.AddDays(7);

        return apiFixtureDate >= roundStartDate && apiFixtureDate < roundEndDate;
    }
}
