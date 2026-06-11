namespace ThePredictions.Contracts.Leagues;

public class WinningsDto
{
    public bool WinningsCalculated { get; init; }
    public int EntryCount { get; init; }
    public decimal EntryCost { get; init; }
    public decimal TotalPrizePot { get; init; }

    public WinningsLeaderboardDto Leaderboard { get; init; } = new();
    public List<PrizeDto> RoundPrizes { get; set; } = [];
    public List<PrizeDto> MonthlyPrizes { get; set; } = [];
    public List<PrizeDto> StagePrizes { get; init; } = [];
    public List<PrizeDto> EndOfSeasonPrizes { get; init; } = [];
}