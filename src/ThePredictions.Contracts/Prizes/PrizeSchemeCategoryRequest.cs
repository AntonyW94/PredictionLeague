using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Prizes;

/// <summary>One enabled category in a prize-scheme request: its per-entry pound allocation and an optional places-table override.</summary>
public class PrizeSchemeCategoryRequest
{
    public PrizeType Category { get; set; }
    public int PerEntryPounds { get; set; }
    public string? RankTableJson { get; set; }
}
