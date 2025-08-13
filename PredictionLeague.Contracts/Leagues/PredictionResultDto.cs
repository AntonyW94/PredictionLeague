namespace PredictionLeague.Contracts.Leagues
{
    public class PredictionResultDto
    {
        public string UserId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public bool HasPredicted { get; set; }
        public int TotalPoints { get; set; }
        public long Rank { get; set; }
        public List<PredictionScoreDto> Predictions { get; set; } = new();
    }
}
