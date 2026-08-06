using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Prizes;

/// <summary>
/// A single payable line in a prize category: a ranked place, a per-event prize, a staged place,
/// or a single prize. <see cref="Delta"/> carries the marginal effect of one more entrant for the
/// prospective-member "+£x" view (null when not computing a delta).
/// </summary>
[ExcludeFromCodeCoverage]
public class PrizeSlotDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int? Rank { get; init; }
    public string? StageName { get; init; }
    public decimal? Delta { get; init; }
}
