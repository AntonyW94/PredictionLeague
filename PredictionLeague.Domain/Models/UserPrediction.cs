using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class UserPrediction
{
    public int Id { get; init; }
    public int MatchId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int PredictedHomeScore { get; private init; }
    public int PredictedAwayScore { get; private init; }
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

        var now = DateTime.UtcNow;

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

    public void CalculatePoints(int actualHomeScore, int actualAwayScore)
    {
        if (PredictedHomeScore == actualHomeScore && PredictedAwayScore == actualAwayScore)
            PointsAwarded = 5;
        else if (Math.Sign(PredictedHomeScore - PredictedAwayScore) == Math.Sign(actualHomeScore - actualAwayScore))
            PointsAwarded = 3;
        else
            PointsAwarded = 0;

        UpdatedAt = DateTime.UtcNow;
    }
}