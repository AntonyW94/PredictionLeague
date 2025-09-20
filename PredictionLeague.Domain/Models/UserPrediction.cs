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

    private UserPrediction() { }

    public static UserPrediction Create(string userId, int matchId, int homeScore, int awayScore)
    {
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));
        Guard.Against.NegativeOrZero(matchId, nameof(matchId));
        Guard.Against.Negative(homeScore, nameof(homeScore));
        Guard.Against.Negative(awayScore, nameof(awayScore));

        var now = DateTime.Now;

        return new UserPrediction
        {
            UserId = userId,
            MatchId = matchId,
            PredictedHomeScore = homeScore,
            PredictedAwayScore = awayScore,
            PointsAwarded = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void CalculatePoints(MatchStatus status, int? actualHomeScore, int? actualAwayScore)
    {
        if (status == MatchStatus.Scheduled || actualHomeScore == null || actualAwayScore == null)
        {
            PointsAwarded = null;
            UpdatedAt = DateTime.Now; 
            return;
        }

        if (PredictedHomeScore == actualHomeScore && PredictedAwayScore == actualAwayScore)
            PointsAwarded = 5;
        else if (Math.Sign(PredictedHomeScore - PredictedAwayScore) == Math.Sign(actualHomeScore.Value - actualAwayScore.Value))
            PointsAwarded = 3;
        else
            PointsAwarded = 0;

        UpdatedAt = DateTime.Now;
    }
}