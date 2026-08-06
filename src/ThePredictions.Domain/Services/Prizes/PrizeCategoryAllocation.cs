using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// One enabled category's input to the apportionment engine: which category, how it is scored
/// (<see cref="Kind"/>), the whole-pound share of each entry it receives, and - for ranked
/// categories (Overall, Section) - the places table to use.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class PrizeCategoryAllocation
{
    public PrizeType Category { get; init; }
    public PrizeCategoryKind Kind { get; init; }
    public int PerEntryPounds { get; init; }
    public RankTable? RankTable { get; init; }
}
