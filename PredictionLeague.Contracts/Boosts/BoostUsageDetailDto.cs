namespace PredictionLeague.Contracts.Boosts;

public sealed class BoostUsageDetailDto
{
    public int RoundNumber { get; init; }
    public int? PointsGained { get; init; }
    public bool IsInProgressRound { get; init; }
}
