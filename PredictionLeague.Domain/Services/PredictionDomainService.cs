using Ardalis.GuardClauses;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Domain.Services;

public class PredictionDomainService
{
    public IEnumerable<UserPrediction> SubmitPredictions(Round round, string userId, IEnumerable<(int MatchId, int HomeScore, int AwayScore)> predictedScores)
    {
        Guard.Against.Null(round, nameof(round));
        
        if (round.Deadline < DateTime.UtcNow)
            throw new InvalidOperationException("The deadline for submitting predictions for this round has passed.");
        
        var predictions = predictedScores.Select(p => UserPrediction.Create(
            userId,
            p.MatchId,
            p.HomeScore,
            p.AwayScore
        )).ToList();

        return predictions;
    }
}