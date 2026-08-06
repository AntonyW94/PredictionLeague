using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public sealed class WindowUsageSummaryDto
{
    public int StartRoundNumber { get; init; }
    public int EndRoundNumber { get; init; }
    public int MaxUsesInWindow { get; init; }
    public bool IsFullSeason { get; init; }
    public bool HasWindowPassed { get; init; }
    public List<PlayerWindowUsageDto> PlayerUsages { get; init; } = [];
}
