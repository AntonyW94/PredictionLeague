namespace PredictionLeague.Core.Models
{
    public class UserPrediction
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int PredictedHomeScore { get; set; }
        public int PredictedAwayScore { get; set; }
        public int? PointsAwarded { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}