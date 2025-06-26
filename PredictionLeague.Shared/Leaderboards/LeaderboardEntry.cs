namespace PredictionLeague.Shared.Leaderboards
{
    public class LeaderboardEntry
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rank { get; set; }
        public int TotalPoints { get; set; }
    }
}
