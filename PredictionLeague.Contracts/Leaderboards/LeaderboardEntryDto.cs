namespace PredictionLeague.Contracts.Leaderboards;

public class LeaderboardEntryDto
{
    public int Rank { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public int TotalPoints { get; init; }
}
