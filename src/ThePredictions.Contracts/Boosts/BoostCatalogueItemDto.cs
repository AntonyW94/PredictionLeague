namespace ThePredictions.Contracts.Boosts;

/// <summary>A boost in the catalogue, shown as a selectable option when configuring a league.</summary>
public class BoostCatalogueItemDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Tooltip { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? SelectedImageUrl { get; init; }
    public string? DisabledImageUrl { get; init; }
}
