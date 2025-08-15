namespace PredictionLeague.Contracts.Leaderboards;

public class LeaderboardEntryDto
{
    public long Rank { get; init; }
    public string PlayerName { get; init; }
    public int? TotalPoints { get; init; }
    public string UserId { get; set; }
}
