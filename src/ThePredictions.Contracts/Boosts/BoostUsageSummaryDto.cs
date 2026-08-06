using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage]
public sealed class BoostUsageSummaryDto
{
    public string BoostCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int TotalUsesPerSeason { get; init; }
    public List<WindowUsageSummaryDto> Windows { get; init; } = [];
}
