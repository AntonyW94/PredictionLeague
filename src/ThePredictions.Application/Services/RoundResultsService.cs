using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Services;

/// <inheritdoc cref="IRoundResultsService"/>
public class RoundResultsService(IUserPredictionRepository userPredictionRepository, IRoundRepository roundRepository)
    : IRoundResultsService
{
    public async Task RecalculateAsync(Round round, CancellationToken cancellationToken)
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
