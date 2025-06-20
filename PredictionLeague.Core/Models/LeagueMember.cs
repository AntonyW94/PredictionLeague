namespace PredictionLeague.Core.Models
{
    public class LeagueMember
    {
        public int LeagueId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }
}