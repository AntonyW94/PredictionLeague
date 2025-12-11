namespace PredictionLeague.Domain.Models;

public record BoostDefinition(
    int BoostDefinitionId,
    string BoostCode,
    string Name,
    string? Tooltip,
    string? Description,
    string? ImageUrl,
    string? SelectedImageUrl,
    string? DisabledImageUrl
);