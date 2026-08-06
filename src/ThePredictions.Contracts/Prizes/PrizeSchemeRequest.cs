using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Prizes;

/// <summary>
/// An admin's prize-scheme configuration: the admin top-up, the £5-rounding threshold, and the
/// per-entry allocation across enabled categories. Submitted at league creation or once via Edit.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class PrizeSchemeRequest
{
    public List<PrizeSchemeCategoryRequest> Categories { get; set; } = [];
}
