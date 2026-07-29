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

            // Stats and prizes are finalised, so award any badges earned this round before the digest
            // goes out. Idempotent, so re-completing the round won't double-award.
            var badgesAwarded = await mediator.Send(new EvaluateBadgesForRoundCommand(round.Id), cancellationToken);

            // Stats and prizes are now finalised for the round, so send the results digest, passing the
            // badges just earned so the email can celebrate them.
            // Idempotent via Round.ResultsDigestSentUtc, so re-completing the round won't re-send.
            await mediator.Send(new SendRoundDigestEmailsCommand(round.Id, BadgesAwarded: badgesAwarded), cancellationToken);

            // Then the celebratory prize emails - winners see "here's how you did" before "and you won!".
            // Idempotent via the PrizeNotifications sent-log, so re-completing the round won't re-send.
            await mediator.Send(new SendPrizeNotificationsCommand(round.Id), cancellationToken);
        }

        // Rebuild the cached My Leagues ranks once, at the very end, so they reflect the final state of
        // this update - the round going live, the new scores, the boosts, and the round completing.
        // The recompute is a pure function of what is now in the database, so a single call covers all
        // of those without caring which of them happened. Nothing above reads these columns, so there
        // is no ordering constraint pulling it earlier.
        await leagueStatsRepository.RefreshSeasonAsync(round.SeasonId, cancellationToken);
    }
}