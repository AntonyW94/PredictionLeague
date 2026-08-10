using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// One boost definition as the database holds it, and the contract between
/// <see cref="IBoostCatalogueQuery"/> and its implementations.
///
/// Lives in Application rather than in an adapter because it is part of the port: a second adapter must
/// return this shape, and the handler must be able to name it without knowing which adapter answered.
/// Nothing here is ordered or formatted - the adapter's job ends at "these are the rows".
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BoostCatalogueRow(
    string Code,
    string Name,
    string? Description,
    string? Tooltip,
    string Scope,
    string? ImageUrl,
    string? SelectedImageUrl,
    string? DisabledImageUrl);
