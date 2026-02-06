namespace PredictionLeague.Contracts.Boosts;

public sealed class WindowUsageSummaryDto
{
    public int StartRoundNumber { get; init; }
    public int EndRoundNumber { get; init; }
    public int MaxUsesInWindow { get; init; }
    public bool IsFullSeason { get; init; }
    public List<PlayerWindowUsageDto> PlayerUsages { get; init; } = [];
}
