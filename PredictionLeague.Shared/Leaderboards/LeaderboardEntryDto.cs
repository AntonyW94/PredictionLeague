namespace PredictionLeague.Shared.Leaderboards;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
}
