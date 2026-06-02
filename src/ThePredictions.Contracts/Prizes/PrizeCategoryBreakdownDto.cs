using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Prizes;

/// <summary>
/// One category's resolved prizes at a given entrant count: the money it holds and its slots.
/// </summary>
public class PrizeCategoryBreakdownDto
{
    public PrizeType Category { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public PrizeCategoryKind Kind { get; init; }
    public decimal SubPot { get; init; }

    /// <summary>The marginal change to this category's sub-pot from one more entrant (null unless computing a delta).</summary>
    public decimal? Delta { get; init; }

    public List<PrizeSlotDto> Slots { get; init; } = [];
}
