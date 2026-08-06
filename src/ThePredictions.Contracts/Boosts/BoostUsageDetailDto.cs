using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public sealed class BoostUsageDetailDto
{
    public int RoundNumber { get; init; }
    public int? PointsGained { get; init; }
    public bool IsInProgressRound { get; init; }
}
