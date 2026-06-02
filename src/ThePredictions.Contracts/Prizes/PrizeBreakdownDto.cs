namespace ThePredictions.Contracts.Prizes;

/// <summary>
/// The full round-number prize breakdown for a scheme at a given entrant count. Category sub-pots
/// always sum to <see cref="Pot"/>.
/// </summary>
public class PrizeBreakdownDto
{
    public decimal Pot { get; init; }
    public int EntrantCount { get; init; }
    public List<PrizeCategoryBreakdownDto> Categories { get; init; } = [];
}
