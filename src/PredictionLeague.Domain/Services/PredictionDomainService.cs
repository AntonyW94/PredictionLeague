using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Domain.Services;

public class PredictionDomainService
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public PredictionDomainService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public IEnumerable<UserPrediction> SubmitPredictions(Round round, string userId, IEnumerable<(int MatchId, int HomeScore, int AwayScore)> predictedScores)
    {
        Guard.Against.Null(round);

        if (round.DeadlineUtc < _dateTimeProvider.UtcNow)
            throw new InvalidOperationException("The deadline for submitting predictions for this round has passed.");

        var predictions = predictedScores.Select(p => UserPrediction.Create(
            userId,
            p.MatchId,
            p.HomeScore,
            p.AwayScore,
            _dateTimeProvider
        )).ToList();

        return predictions;
    }
}
