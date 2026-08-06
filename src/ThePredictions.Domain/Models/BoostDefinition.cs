using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public record BoostDefinition(
    string BoostCode,
    string Name,
    string? Tooltip,
    string? Description,
    string? ImageUrl,
    string? SelectedImageUrl,
    string? DisabledImageUrl
);
