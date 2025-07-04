namespace PredictionLeague.Domain.Models;

public class UserPrediction
{
    public int Id { get; init; }
    public int MatchId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public int PredictedHomeScore { get; init; }
    public int PredictedAwayScore { get; init; }
    public int? PointsAwarded { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}