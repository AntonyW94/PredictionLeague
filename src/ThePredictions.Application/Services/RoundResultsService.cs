using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Services;

/// <inheritdoc cref="IRoundResultsService"/>
public class RoundResultsService(
    IUserPredictionRepository userPredictionRepository,
    IRoundRepository roundRepository,
    ILeagueRepository leagueRepository) : IRoundResultsService
{
    public async Task RecalculateAsync(Round round, CancellationToken cancellationToken)
    {
        await RecalculateTalliesAsync(round, cancellationToken);
        await RecalculateLeaguePointsAsync(round, cancellationToken);
    }

    /// <summary>
    /// What each player's round is worth in each of their leagues.
    /// </summary>
    /// <remarks>
    /// After the tallies, because it reads them. Two leagues watching the same fixtures award different totals for the
    /// same predictions, which is why this is per (league, player) rather than per player.
    /// </remarks>
    private async Task RecalculateLeaguePointsAsync(Round round, CancellationToken cancellationToken)
    {
        var inputs = await leagueRepository.GetLeagueRoundScoringInputsAsync(round.Id, cancellationToken);

        var scores = inputs
            .Select(input =>
            {
                var basePoints = LeagueScoring.BasePoints(
                    input.Counts, input.PointsForExactScore, input.PointsForCorrectResult);

                // Boosted points start equal to base points, and any boost is cleared: boosts are applied in the step
                // after this one, so one left on the row would be counted again every time a round was re-processed.
                return new LeagueRoundScore(
                    input.LeagueId, input.UserId, basePoints, basePoints, HasBoost: false, AppliedBoostCode: null);
            })
            .ToList();

        await leagueRepository.UpdateLeagueRoundResultsAsync(round.Id, scores, cancellationToken);
    }

    private async Task RecalculateTalliesAsync(Round round, CancellationToken cancellationToken)
    {
        var matchIds = round.Matches.Select(match => match.Id).ToList();

        if (matchIds.Count == 0)
            return;

        var predictions = await userPredictionRepository.GetByMatchIdsAsync(matchIds, cancellationToken);

        // Every fixture in the round, not only the ones whose scores just changed: a tally is the whole round's answer for
        // that player, so recomputing it from a subset would report the last few fixtures as their entire round.
        var tallies = predictions
            .GroupBy(prediction => prediction.UserId)
            .Select(group => new RoundResultTally(
                group.Key,
                OutcomeTally.For(group.Select(prediction => prediction.Outcome))))
            .ToList();

        await roundRepository.UpdateRoundResultsAsync(round.Id, tallies, cancellationToken);
    }
}
