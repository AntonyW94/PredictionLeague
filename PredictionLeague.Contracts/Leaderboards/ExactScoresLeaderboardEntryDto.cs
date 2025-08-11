namespace PredictionLeague.Contracts.Leaderboards;

public class ExactScoresLeaderboardEntryDto
{
    public long Rank { get; set; }
    public string PlayerName { get; init; } = null!;
    public int ExactScoresCount { get; init; }
}