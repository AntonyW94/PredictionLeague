namespace PredictionLeague.Contracts.Leaderboards;

public class LeaderboardEntryDto
{
    public long Rank { get; init; }
    public required string PlayerName { get; init; }
    public int? TotalPoints { get; init; }
    public required string UserId { get; init; }
}
