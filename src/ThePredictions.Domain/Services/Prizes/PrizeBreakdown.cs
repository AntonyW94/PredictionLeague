using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// The full, round-number prize breakdown for a scheme evaluated at a given entrant count.
/// The category slot amounts always sum to <see cref="PotPounds"/> exactly (money is conserved).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class PrizeBreakdown
{
    /// <summary>The total pot: <c>StakePounds * EntrantCount + AdminTopUpPounds</c>.</summary>
    public int PotPounds { get; init; }

    public IReadOnlyList<PrizeCategoryBreakdown> Categories { get; init; } = [];
}
