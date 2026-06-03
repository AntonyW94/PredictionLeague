using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// All the inputs the pure apportionment engine needs to turn a prize scheme into a concrete,
/// round-number breakdown at a given entrant count. Whole pounds throughout.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PrizeApportionmentRequest
{
    /// <summary>Approved entrant count (N). The pot grows in whole stakes as this rises.</summary>
    public int EntrantCount { get; init; }

    /// <summary>The per-entry stake (E), in whole pounds. Per-category allocations sum to this.</summary>
    public int StakePounds { get; init; }

    /// <summary>Money the admin puts up on top of the entry fees, in whole pounds.</summary>
    public int AdminTopUpPounds { get; init; }

    /// <summary>Number of prediction rounds in the season (the Round category's event count).</summary>
    public int NumberOfRounds { get; init; }

    /// <summary>Number of calendar months the season spans (the Monthly category's event count).</summary>
    public int NumberOfMonths { get; init; }

    /// <summary>The enabled categories and their per-entry allocations.</summary>
    public IReadOnlyList<PrizeCategoryAllocation> Categories { get; init; } = [];
}
