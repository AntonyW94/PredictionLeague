using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommandHandler(
    IMediator mediator,
    IBoostService boostService,
    IRoundResultsService roundResultsService,
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository,
    IUserPredictionRepository userPredictionRepository,
    ILeagueStatsRepository leagueStatsRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateMatchResultsCommand>
{
    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        // If user is authenticated (admin UI call), verify they're an administrator.
        // If not authenticated (scheduled task via API key), skip the check.
        if (currentUserService.IsAuthenticated)
            currentUserService.EnsureAdministrator();

        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        Guard.Against.EntityNotFound(request.RoundId, round, "Round");
        var wasRoundPublished = round.Status == RoundStatus.Published;

        var matchesToUpdate = ApplyScores(round, request);

        if (!matchesToUpdate.Any())
            return;

        var isRoundStarting = wasRoundPublished && matchesToUpdate.Any(m => m.Status is MatchStatus.InProgress or MatchStatus.Completed);
        if (isRoundStarting)
        {
            round.UpdateStatus(RoundStatus.InProgress, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);

            var isLastRoundOfSeason = await roundRepository.IsLastRoundOfSeasonAsync(round.Id, round.SeasonId, cancellationToken);
            if (isLastRoundOfSeason)
                await boostService.AutoApplyUnusedBoostsForLastRoundAsync(round.Id, cancellationToken);
        }

        await roundRepository.UpdateMatchScoresAsync(matchesToUpdate, cancellationToken);
        await ScorePredictionsAsync(matchesToUpdate, cancellationToken);
        await roundResultsService.RecalculateAsync(round, cancellationToken);
        await boostService.ApplyRoundBoostsAsync(round.Id, cancellationToken);
        
        var isRoundFinishing = round.Matches.All(m => m.Status is MatchStatus.Completed or MatchStatus.Postponed);
        if (isRoundFinishing)
        {
            round.UpdateStatus(RoundStatus.Completed, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);
        }

        // Rebuild the cached ranks here, and only here. It has to be after the round's own status change,
        // because the recompute resolves which round the cache describes from the current statuses. And it
        // has to be before the round-completion work below, because the results digest email reads
        // [OverallRank] and [SnapshotOverallRank] to tell each player their position and how far they
        // moved (GetRoundDigestQueryHandler) - run the refresh after that and the email reports the
        // previous update's figures.
        await leagueStatsRepository.RefreshSeasonAsync(round.SeasonId, cancellationToken);

        if (isRoundFinishing)
            await CompleteRoundAsync(round, cancellationToken);
    }

    /// <summary>
    /// Applies each supplied score to the fixture it names, ignoring any that is not in this round,
    /// and returns just the fixtures that actually changed.
    /// </summary>
    private static List<Match> ApplyScores(Round round, UpdateMatchResultsCommand request)
    {
        var matchesToUpdate = new List<Match>();

        foreach (var matchResult in request.Matches)
        {
            var matchToUpdate = round.Matches.FirstOrDefault(m => m.Id == matchResult.MatchId);
            if (matchToUpdate == null)
                continue;

            matchToUpdate.UpdateScore(matchResult.HomeScore, matchResult.AwayScore, matchResult.Status);
            matchesToUpdate.Add(matchToUpdate);
        }

        return matchesToUpdate;
    }

    /// <summary>Re-scores every prediction against the fixtures whose scores just moved.</summary>
    private async Task ScorePredictionsAsync(List<Match> matchesToUpdate, CancellationToken cancellationToken)
    {
        var matchIds = matchesToUpdate.Select(m => m.Id).ToList();
        var predictionsToUpdate = (await userPredictionRepository.GetByMatchIdsAsync(matchIds, cancellationToken)).ToList();

        foreach (var prediction in predictionsToUpdate)
        {
            var match = matchesToUpdate.FirstOrDefault(m => m.Id == prediction.MatchId);
            if (match == null)
                continue;

            prediction.SetOutcome(match.Status, match.ActualHomeTeamScore, match.ActualAwayTeamScore, dateTimeProvider);
        }

        await userPredictionRepository.UpdateOutcomesAsync(predictionsToUpdate, cancellationToken);
    }

    /// <summary>
    /// Settles prizes, awards badges, then sends the results digest and the prize emails - in that
    /// order, so winners see "here's how you did" before "and you won!". Every step is idempotent, so
    /// re-completing the round neither double-awards nor re-sends.
    /// </summary>
    private async Task CompleteRoundAsync(Round round, CancellationToken cancellationToken)
    {
        var leagueIds = await leagueRepository.GetLeagueIdsForSeasonAsync(round.SeasonId, cancellationToken);

        foreach (var leagueId in leagueIds)
        {
            await mediator.Send(new ProcessPrizesCommand { RoundId = round.Id, LeagueId = leagueId }, cancellationToken);
        }

        var badgesAwarded = await mediator.Send(new EvaluateBadgesForRoundCommand(round.Id), cancellationToken);

        await mediator.Send(new SendRoundDigestEmailsCommand(round.Id, BadgesAwarded: badgesAwarded), cancellationToken);
        await mediator.Send(new SendPrizeNotificationsCommand(round.Id), cancellationToken);
    }
}