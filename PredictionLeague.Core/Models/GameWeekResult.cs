namespace PredictionLeague.Core.Models
{
    public class GameWeekResult
    {
        public int Id { get; set; }
        public int GameWeekId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int TotalPoints { get; set; }
    }
}