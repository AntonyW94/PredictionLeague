namespace PredictionLeague.Contracts.Leagues;

public class WinningsLeaderboardEntryDto
{
    public string PlayerName { get; set; } = string.Empty;
    public decimal RoundWinnings { get; set; }
    public decimal MonthlyWinnings { get; set; }
    public decimal EndOfSeasonWinnings { get; set; }
    public decimal TotalWinnings { get; set; }
}