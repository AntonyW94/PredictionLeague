using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class UserPrediction
{
    public int Id { get; init; }
    public int MatchId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int PredictedHomeScore { get; init; }
    public int PredictedAwayScore { get; init; }
    public int? PointsAwarded { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public PredictionOutcome Outcome { get; private set; } = PredictionOutcome.Pending;

    private UserPrediction() { }

    public static UserPrediction Create(string userId, int matchId, int homeScore, int awayScore)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(matchId);
        Guard.Against.Negative(homeScore);
        Guard.Against.Negative(awayScore);

        var now = DateTime.Now;

        return new UserPrediction
        {
            UserId = userId,
            MatchId = matchId,
            PredictedHomeScore = homeScore,
            PredictedAwayScore = awayScore,
            PointsAwarded = null,
            CreatedAt = now,
            UpdatedAt = now,
            Outcome = PredictionOutcome.Pending
        };
    }
    
    public void CalculatePoints(MatchStatus status, int? actualHomeScore, int? actualAwayScore, int correctScorePoints = 5, int correctResultPoints = 3)
    {
        if (status == MatchStatus.Scheduled || actualHomeScore == null || actualAwayScore == null)
        {
            PointsAwarded = null;
            Outcome = PredictionOutcome.Pending;
            UpdatedAt = DateTime.Now;
            return;
        }

        if (PredictedHomeScore == actualHomeScore && PredictedAwayScore == actualAwayScore)
        {
            Outcome = PredictionOutcome.CorrectScore;
            PointsAwarded = correctScorePoints;
        }
        else if (Math.Sign(PredictedHomeScore - PredictedAwayScore) == Math.Sign(actualHomeScore.Value - actualAwayScore.Value))
        {
            Outcome = PredictionOutcome.CorrectResult;
            PointsAwarded = correctResultPoints;
        }
        else
        {
            Outcome = PredictionOutcome.Incorrect;
            PointsAwarded = 0;
        }

        UpdatedAt = DateTime.Now;
    }
}