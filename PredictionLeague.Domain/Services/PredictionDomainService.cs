using Ardalis.GuardClauses;
using PredictionLeague.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Services;

[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
public class PredictionDomainService
{
    public IEnumerable<UserPrediction> SubmitPredictions(Round round, string userId, IEnumerable<(int MatchId, int HomeScore, int AwayScore)> predictedScores)
    {
        Guard.Against.Null(round);
        
        if (round.Deadline < DateTime.Now)
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