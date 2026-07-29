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
    ILeagueStatsRepository leagueStatsRepository,
    IMediator mediator) : IRequestHandler<UpdateScoresForNextRoundCommand>
{
    public async Task Handle(UpdateScoresForNextRoundCommand request, CancellationToken cancellationToken)
    {
        // Rebuild the season's cached My Leagues ranks on every tick, before doing anything else.
        //
        // This is the backstop that makes the cache trustworthy rather than merely usually-right. The
        // explicit triggers (results updates, membership changes) keep it correct immediately; this
        // catches everything they cannot see, in particular the parts of the tile that change with the
        // passage of time rather than with an event - the active round rolling on to the next month or
        // stage, and a completed round ageing out of the 48-hour window during which it stays the round
        // the tile is showing. Neither of those fires a write anywhere in the system.
        //
        // It runs unconditionally, not only when rows were created, so the worst-case staleness is one
        // minute. The recompute is a handful of set-based statements over one season, so a tick that
        // finds nothing to change is cheap.
        await leagueStatsRepository.RefreshSeasonAsync(request.SeasonId, cancellationToken);

        var activeRound = await roundRepository.GetOldestInProgressRoundAsync(request.SeasonId, cancellationToken);
        if (activeRound == null || !activeRound.Matches.Any())
            return;

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