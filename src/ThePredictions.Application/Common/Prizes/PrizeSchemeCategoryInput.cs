using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>One enabled category as supplied to the evaluator (from a saved scheme or a draft).</summary>
public sealed class PrizeSchemeCategoryInput
{
    public PrizeType Category { get; init; }
    public int PerEntryPounds { get; init; }
    public string? RankTableJson { get; init; }
}
