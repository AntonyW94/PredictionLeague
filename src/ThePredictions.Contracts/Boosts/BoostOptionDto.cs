using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class BoostOptionDto
{
    public string BoostCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Tooltip { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string SelectedImageUrl { get; init; } = string.Empty;
    public string DisabledImageUrl { get; init; } = string.Empty;
    public BoostEligibilityDto? Eligibility { get; init; }
}
