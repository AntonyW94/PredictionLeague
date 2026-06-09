using Ardalis.GuardClauses;
using MediatR;
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
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository,
    IUserPredictionRepository userPredictionRepository,
    ILeagueStatsService statsService,
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

        var completedMatchIdsBefore = round.Matches
            .Where(m => m.Status == MatchStatus.Completed)
            .Select(m => m.Id)
            .ToList();

        var matchesToUpdate = new List<Match>();

        foreach (var matchResult in request.Matches)
        {
            var matchToUpdate = round.Matches.FirstOrDefault(m => m.Id == matchResult.MatchId);
            if (matchToUpdate == null)
                continue;

            matchToUpdate.UpdateScore(matchResult.HomeScore, matchResult.AwayScore, matchResult.Status);
            matchesToUpdate.Add(matchToUpdate);
        }

        if (!matchesToUpdate.Any())
            return;

        var isRoundStarting = wasRoundPublished && matchesToUpdate.Any(m => m.Status is MatchStatus.InProgress or MatchStatus.Completed);
        if (isRoundStarting)
        {
            round.UpdateStatus(RoundStatus.InProgress, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);
            await statsService.TakeRoundStartSnapshotsAsync(round.Id, cancellationToken);

            var isLastRoundOfSeason = await roundRepository.IsLastRoundOfSeasonAsync(round.Id, round.SeasonId, cancellationToken);
            if (isLastRoundOfSeason)
                await boostService.AutoApplyUnusedBoostsForLastRoundAsync(round.Id, cancellationToken);
        }

        await roundRepository.UpdateMatchScoresAsync(matchesToUpdate, cancellationToken);

        var matchIds = matchesToUpdate.Select(m => m.Id).ToList();
      
        var predictionsToUpdate = (await userPredictionRepository.GetByMatchIdsAsync(matchIds, cancellationToken)).ToList();
        if (predictionsToUpdate.Any())
        {
            foreach (var prediction in predictionsToUpdate)
            {
                var match = matchesToUpdate.FirstOrDefault(m => m.Id == prediction.MatchId);
                if (match == null)
                    continue;

                prediction.SetOutcome(match.Status, match.ActualHomeTeamScore, match.ActualAwayTeamScore, dateTimeProvider);
            }
        }
        
        await userPredictionRepository.UpdateOutcomesAsync(predictionsToUpdate, cancellationToken);
        await roundRepository.UpdateRoundResultsAsync(round.Id, cancellationToken);
        await leagueRepository.UpdateLeagueRoundResultsAsync(round.Id, cancellationToken);
        await boostService.ApplyRoundBoostsAsync(round.Id, cancellationToken);
        
        var hasNewCompletedMatch = matchesToUpdate.Any(m => m.Status == MatchStatus.Completed && !completedMatchIdsBefore.Contains(m.Id));
        if (hasNewCompletedMatch)
            await statsService.UpdateStableStatsAsync(round.Id, cancellationToken);
        
        await statsService.UpdateLiveStatsAsync(round.Id, cancellationToken);
      
        if (round.Matches.All(m => m.Status is MatchStatus.Completed or MatchStatus.Postponed))
        {
            round.UpdateStatus(RoundStatus.Completed, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);

            var leagueIds = await leagueRepository.GetLeagueIdsForSeasonAsync(round.SeasonId, cancellationToken);

            foreach (var leagueId in leagueIds)
            {
                var processPrizesCommand = new ProcessPrizesCommand
                {
                    RoundId = round.Id,
                    LeagueId = leagueId
                };
                await mediator.Send(processPrizesCommand, cancellationToken);
            }

            // Stats and prizes are now finalised for the round, so send the results digest.
            // Idempotent via Round.ResultsDigestSentUtc, so re-completing the round won't re-send.
            await mediator.Send(new SendRoundDigestEmailsCommand(round.Id), cancellationToken);

            // Then the celebratory prize emails - winners see "here's how you did" before "and you won!".
            // Idempotent via the PrizeNotifications sent-log, so re-completing the round won't re-send.
            await mediator.Send(new SendPrizeNotificationsCommand(round.Id), cancellationToken);
        }
    }
}