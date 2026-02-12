namespace PredictionLeague.Domain.Models;

public record BoostDefinition(
    string BoostCode,
    string Name,
    string? Tooltip,
    string? Description,
    string? ImageUrl,
    string? SelectedImageUrl,
    string? DisabledImageUrl
);