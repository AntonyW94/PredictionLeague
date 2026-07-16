using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Domain.Services;

public class PredictionDomainService(IDateTimeProvider dateTimeProvider)
{
    public IEnumerable<UserPrediction> SubmitPredictions(Round round, string userId, IEnumerable<(int MatchId, int HomeScore, int AwayScore)> predictedScores)
    {
        Guard.Against.Null(round);

        var utcNow = dateTimeProvider.UtcNow;

        // Reject only once every match has locked. While any match is still open (for example the final and
        // third-place playoff of a combined round that carry a later custom lock than the round deadline that
        // locked the semi-finals), the per-match filter below keeps the already-locked matches untouched and
        // accepts predictions for the open ones.
        if (round.IsClosedForPredictions(utcNow))
            throw new InvalidOperationException("The deadline for submitting predictions for this round has passed.");

        var matchesById = round.Matches.ToDictionary(m => m.Id);

        var predictions = predictedScores
            .Where(p =>
                matchesById.TryGetValue(p.MatchId, out var match) &&
                match.AreTeamsConfirmed &&
                !match.IsPredictionLocked(utcNow, round.DeadlineUtc))
            .Select(p => UserPrediction.Create(
                userId,
                p.MatchId,
                p.HomeScore,
                p.AwayScore,
                dateTimeProvider
            )).ToList();

        return predictions;
    }
}
