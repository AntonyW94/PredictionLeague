using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// A single payable line in a category: a ranked place ("1st"), a per-event prize ("Per round"),
/// a staged place ("Group stage - 1st"), or a single prize ("Most exact scores").
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class PrizeBreakdownSlot
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }

    /// <summary>The rank this slot pays (1 = winner). Null for per-event prizes.</summary>
    public int? Rank { get; init; }

    /// <summary>The tournament stage this slot belongs to (Section only); otherwise null.</summary>
    public string? StageName { get; init; }
}
