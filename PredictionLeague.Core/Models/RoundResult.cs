namespace PredictionLeague.Core.Models
{
    public class RoundResult
    {
        public int Id { get; set; }
        public int RoundId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int TotalPoints { get; set; }
    }
}