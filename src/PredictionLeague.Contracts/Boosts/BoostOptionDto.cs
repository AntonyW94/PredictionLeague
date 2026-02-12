namespace PredictionLeague.Contracts.Boosts;

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