using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

[ExcludeFromCodeCoverage]
public record BoostDefinition(
    string BoostCode,
    string Name,
    string? Tooltip,
    string? Description,
    string? ImageUrl,
    string? SelectedImageUrl,
    string? DisabledImageUrl
);