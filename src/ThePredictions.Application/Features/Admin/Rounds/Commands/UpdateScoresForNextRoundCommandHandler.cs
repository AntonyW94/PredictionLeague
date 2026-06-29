using MediatR;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class UpdateScoresForNextRoundCommandHandler(
    IRoundRepository roundRepository,
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    IFootballDataService footballDataService,
    ILeagueStatsService statsService,
    IMediator mediator) : IRequestHandler<UpdateScoresForNextRoundCommand>
{
    public async Task Handle(UpdateScoresForNextRoundCommand request, CancellationToken cancellationToken)
    {
        var activeRound = await roundRepository.GetOldestInProgressRoundAsync(request.SeasonId, cancellationToken);
        if (activeRound == null || !activeRound.Matches.Any())
            return;

        // Self-heal any league member who is missing a cached stats row for this in-progress round.
        // Rows are normally created when the round goes live, but a round that started before that
        // logic existed never got them, leaving the My Leagues round/overall tiles blank or wrong.
        // This runs on every tick while the round is in progress but only does the (cheap) rank
        // recompute when rows were actually created, so it is a no-op once everyone has a row.
        var createdStatsRows = await statsService.EnsureMemberStatsRowsExistAsync(activeRound.Id, cancellationToken);
        if (createdStatsRows > 0)
        {
            await statsService.UpdateStableStatsAsync(activeRound.Id, cancellationToken);
            await statsService.UpdateLiveStatsAsync(activeRound.Id, cancellationToken);
        }

        var matchesToCheck = activeRound.Matches
            .Where(m => m.MatchDateTimeUtc < DateTime.UtcNow && m.Status != MatchStatus.Completed)
            .ToList();

        if (!matchesToCheck.Any())
            return;

        var externalIds = matchesToCheck
            .Where(m => m.ExternalId.HasValue)
            .Select(m => m.ExternalId.GetValueOrDefault())
            .ToList();

        if (!externalIds.Any())
            return;

        var liveFixtures = (await footballDataService.GetFixturesByIdsAsync(externalIds, cancellationToken)).ToList();
        if (!liveFixtures.Any())
            return;

        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        var competition = season == null
            ? null
            : await competitionRepository.GetByIdAsync(season.CompetitionId, cancellationToken);
        var isTournament = competition?.IsTournament ?? false;

        var matchResults = liveFixtures.Where(f => f.Fixture != null && f.Goals != null).Select(fixture =>
        {
            var localMatch = activeRound.Matches.First(m => m.ExternalId == fixture.Fixture!.Id);
            var isKnockout = isTournament && IsKnockoutMatch(localMatch);
            var (homeScore, awayScore) = GetScoreForMatch(fixture, isKnockout);
            return new MatchResultDto(
                localMatch.Id,
                homeScore,
                awayScore,
                GetMatchStatus(fixture.Fixture!.Status.Short, isKnockout)
            );
        }).ToList();

        if (matchResults.Any())
        {
            var updateCommand = new UpdateMatchResultsCommand(activeRound.Id, matchResults);
            await mediator.Send(updateCommand, cancellationToken);
        }
    }

    internal static (int HomeScore, int AwayScore) GetScoreForMatch(FixtureResponse fixture, bool isKnockout)
    {
        if (isKnockout)
        {
            var fulltime = fixture.Score?.FullTime;
            if (fulltime?.Home != null && fulltime.Away != null)
                return (fulltime.Home.Value, fulltime.Away.Value);
        }

        return (fixture.Goals!.Home.GetValueOrDefault(), fixture.Goals.Away.GetValueOrDefault());
    }

    internal static bool IsKnockoutMatch(Match match)
    {
        if (string.IsNullOrWhiteSpace(match.ApiRoundName))
            return false;

        if (!TournamentRoundNameParser.TryParseStage(match.ApiRoundName, out var stage))
            return false;

        return TournamentRoundNameParser.IsKnockoutStage(stage);
    }

    internal static MatchStatus GetMatchStatus(string apiStatus, bool isKnockout) => apiStatus switch
    {
        "FT" or "AET" or "PEN" => MatchStatus.Completed,
        // A knockout tie is scored on the 90-minute result, so once regular time is over the
        // result can no longer change for prediction purposes. Treat the break before extra
        // time (BT), extra time (ET) and the penalty shootout (P) as Completed rather than
        // leaving the match live to the final whistle of the tie - this also lets end-of-round
        // processing fire as soon as the last match is decided on 90 minutes.
        "BT" or "ET" or "P" when isKnockout => MatchStatus.Completed,
        "HT" or "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => MatchStatus.InProgress,
        "PST" => MatchStatus.Postponed,
        _ => MatchStatus.Scheduled
    };
}