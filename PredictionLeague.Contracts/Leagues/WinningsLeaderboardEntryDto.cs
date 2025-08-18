namespace PredictionLeague.Contracts.Leagues;

public class WinningsLeaderboardEntryDto
{
    public string PlayerName { get; init; } = string.Empty;
    public decimal RoundWinnings { get; init; }
    public decimal MonthlyWinnings { get; init; }
    public decimal EndOfSeasonWinnings { get; init; }
    public decimal TotalWinnings { get; init; }
    public required string UserId { get; init; }
}