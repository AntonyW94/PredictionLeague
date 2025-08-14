namespace PredictionLeague.Contracts.Leagues;

public class WinningsDto
{
    public bool WinningsCalculated { get; set; }
    public int EntryCount { get; set; }
    public decimal EntryCost { get; set; }
    public decimal TotalPrizePot { get; set; }

    public WinningsLeaderboardDto Leaderboard { get; set; } = new();
    public List<PrizeDto> RoundPrizes { get; set; } = new();
    public List<PrizeDto> MonthlyPrizes { get; set; } = new();
    public List<PrizeDto> EndOfSeasonPrizes { get; set; } = new();
}