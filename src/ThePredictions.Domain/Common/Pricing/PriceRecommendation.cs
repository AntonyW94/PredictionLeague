using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Common.Pricing;

/// <summary>
/// The output of <see cref="PriceRecommendationCalculator"/>: a suggested Standard price for a season
/// plus an explainable breakdown. Always advisory - the admin can override it. When there is no
/// comparable prior season to derive player numbers, <see cref="SuggestedStandardPrice"/> is null and
/// <see cref="UnavailableReason"/> explains why.
/// </summary>
[ExcludeFromCodeCoverage]
public class PriceRecommendation
{
    public PriceRecommendation(
        decimal? suggestedStandardPrice,
        string? unavailableReason,
        decimal annualRunningCost,
        int seasonRounds,
        int totalPaidRoundsInHorizon,
        decimal weight,
        decimal apportionedCost,
        decimal bufferRate,
        decimal targetWithBuffer,
        int? expectedPlayers,
        decimal? perPlayer,
        decimal? feeGrossedUp,
        bool floorApplied)
    {
        SuggestedStandardPrice = suggestedStandardPrice;
        UnavailableReason = unavailableReason;
        AnnualRunningCost = annualRunningCost;
        SeasonRounds = seasonRounds;
        TotalPaidRoundsInHorizon = totalPaidRoundsInHorizon;
        Weight = weight;
        ApportionedCost = apportionedCost;
        BufferRate = bufferRate;
        TargetWithBuffer = targetWithBuffer;
        ExpectedPlayers = expectedPlayers;
        PerPlayer = perPlayer;
        FeeGrossedUp = feeGrossedUp;
        FloorApplied = floorApplied;
    }

    /// <summary>Suggested Standard price, rounded to a tidy figure; null when no comparable season exists.</summary>
    public decimal? SuggestedStandardPrice { get; }

    /// <summary>Explanation shown when no price could be suggested; null when a price is suggested.</summary>
    public string? UnavailableReason { get; }

    /// <summary>Total annual running cost across all recorded costs.</summary>
    public decimal AnnualRunningCost { get; }

    public int SeasonRounds { get; }

    /// <summary>Sum of rounds across all paid seasons sharing the cost horizon (includes this season).</summary>
    public int TotalPaidRoundsInHorizon { get; }

    /// <summary>This season's share of the annual cost (SeasonRounds / TotalPaidRoundsInHorizon).</summary>
    public decimal Weight { get; }

    /// <summary>AnnualRunningCost x Weight.</summary>
    public decimal ApportionedCost { get; }

    public decimal BufferRate { get; }

    /// <summary>ApportionedCost grossed up by the buffer - the amount we aim to recover this season.</summary>
    public decimal TargetWithBuffer { get; }

    /// <summary>Break-even denominator: distinct approved players of the last comparable season; null when unknown.</summary>
    public int? ExpectedPlayers { get; }

    /// <summary>TargetWithBuffer / ExpectedPlayers before fees; null when ExpectedPlayers is unknown.</summary>
    public decimal? PerPlayer { get; }

    /// <summary>Per-player figure grossed up for Stripe fees, before rounding/floor; null when unknown.</summary>
    public decimal? FeeGrossedUp { get; }

    /// <summary>True when the minimum floor lifted the suggestion above the computed figure.</summary>
    public bool FloorApplied { get; }
}
