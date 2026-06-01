using Ardalis.GuardClauses;

namespace ThePredictions.Domain.Common.Pricing;

/// <summary>
/// Pure calculator for the recommended Standard Season Pass price (ADR 0006).
///
/// Recommended price = this season's apportioned share of the business-borne annual running costs,
/// plus a buffer, divided by the expected number of players (break-even at the last comparable
/// season's participant count), grossed up for Stripe fees, lifted to a small floor and rounded up
/// to a tidy figure. All inputs are supplied by the caller so this stays deterministic and testable.
/// </summary>
public static class PriceRecommendationCalculator
{
    public const string NoComparableSeasonReason =
        "Not enough history to suggest a price yet - there's no completed prior season for this competition to estimate player numbers from. Set a price manually.";

    /// <param name="businessBorneAnnualCost">Annual running costs currently borne by the business (>= 0).</param>
    /// <param name="seasonRounds">Number of rounds in the season being priced (>= 1).</param>
    /// <param name="totalPaidRoundsInHorizon">Sum of rounds across all paid seasons sharing the cost horizon, including this season (>= seasonRounds).</param>
    /// <param name="expectedPlayers">Distinct approved players of the last comparable season; null/0 when there is no comparable season.</param>
    /// <param name="bufferRate">Fractional buffer added on top of costs, e.g. 0.15 for 15% (>= 0).</param>
    /// <param name="stripePercent">Stripe percentage fee as a fraction, e.g. 0.015 for 1.5% (0 to &lt; 1).</param>
    /// <param name="stripeFixedFee">Stripe fixed fee per transaction, e.g. 0.20 (>= 0).</param>
    /// <param name="minimumFloor">Smallest price to suggest, covering fees plus a little (>= 0).</param>
    /// <param name="roundingIncrement">Increment to round the suggestion up to, e.g. 0.50 (> 0).</param>
    public static PriceRecommendation Recommend(
        decimal businessBorneAnnualCost,
        int seasonRounds,
        int totalPaidRoundsInHorizon,
        int? expectedPlayers,
        decimal bufferRate,
        decimal stripePercent,
        decimal stripeFixedFee,
        decimal minimumFloor,
        decimal roundingIncrement)
    {
        Guard.Against.Negative(businessBorneAnnualCost);
        Guard.Against.NegativeOrZero(seasonRounds);
        Guard.Against.OutOfRange(totalPaidRoundsInHorizon, nameof(totalPaidRoundsInHorizon), seasonRounds, int.MaxValue);
        Guard.Against.Negative(bufferRate);
        Guard.Against.OutOfRange(stripePercent, nameof(stripePercent), 0m, 0.99m);
        Guard.Against.Negative(stripeFixedFee);
        Guard.Against.Negative(minimumFloor);
        Guard.Against.NegativeOrZero(roundingIncrement);

        var weight = (decimal)seasonRounds / totalPaidRoundsInHorizon;
        var apportionedCost = businessBorneAnnualCost * weight;
        var targetWithBuffer = apportionedCost * (1 + bufferRate);

        if (expectedPlayers is null or <= 0)
        {
            return new PriceRecommendation(
                suggestedStandardPrice: null,
                unavailableReason: NoComparableSeasonReason,
                businessBorneAnnualCost: businessBorneAnnualCost,
                seasonRounds: seasonRounds,
                totalPaidRoundsInHorizon: totalPaidRoundsInHorizon,
                weight: weight,
                apportionedCost: apportionedCost,
                bufferRate: bufferRate,
                targetWithBuffer: targetWithBuffer,
                expectedPlayers: null,
                perPlayer: null,
                feeGrossedUp: null,
                floorApplied: false);
        }

        var perPlayer = targetWithBuffer / expectedPlayers.Value;
        var feeGrossedUp = (perPlayer + stripeFixedFee) / (1 - stripePercent);

        var floorApplied = feeGrossedUp < minimumFloor;
        var afterFloor = Math.Max(feeGrossedUp, minimumFloor);
        var suggested = Math.Ceiling(afterFloor / roundingIncrement) * roundingIncrement;

        return new PriceRecommendation(
            suggestedStandardPrice: suggested,
            unavailableReason: null,
            businessBorneAnnualCost: businessBorneAnnualCost,
            seasonRounds: seasonRounds,
            totalPaidRoundsInHorizon: totalPaidRoundsInHorizon,
            weight: weight,
            apportionedCost: apportionedCost,
            bufferRate: bufferRate,
            targetWithBuffer: targetWithBuffer,
            expectedPlayers: expectedPlayers,
            perPlayer: perPlayer,
            feeGrossedUp: feeGrossedUp,
            floorApplied: floorApplied);
    }
}
