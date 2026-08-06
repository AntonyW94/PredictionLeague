using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// The resolved prizes for one category at a given entrant count: the money held by the category
/// (<see cref="SubPotPounds"/>, after any spillover in or out) and its payable slots.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class PrizeCategoryBreakdown
{
    public PrizeType Category { get; init; }
    public PrizeCategoryKind Kind { get; init; }
    public int SubPotPounds { get; init; }
    public IReadOnlyList<PrizeBreakdownSlot> Slots { get; init; } = [];
}
