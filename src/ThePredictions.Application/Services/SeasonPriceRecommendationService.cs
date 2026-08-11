using Ardalis.GuardClauses;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Pricing;
using ThePredictions.Domain.Models;
using DomainPricingSettings = ThePredictions.Domain.Models.PricingSettings;

namespace ThePredictions.Application.Services;

/// <summary>
/// What to charge for a season: the running costs it has to cover, shared across the paid seasons running alongside it, and
/// divided by however many players last time suggests.
/// </summary>
/// <remarks>
/// Five reads became two. Three of them - the pricing settings, the Stripe fee and the running costs - turned out to be the
/// same reads the administrator's own screens make, so this now goes through those ports rather than repeating their SQL. The
/// pricing settings read was a second copy of <c>TOP 1 ORDER BY [Id]</c>, with nothing tying the two together.
/// </remarks>
public class SeasonPriceRecommendationService(
    IPricingSettingsQuery pricingSettingsQuery,
    IServiceFeesQuery serviceFeesQuery,
    IRunningCostsQuery runningCostsQuery,
    ISeasonPricingQuery seasonPricingQuery,
    IDateTimeProvider dateTimeProvider) : ISeasonPriceRecommendationService
{
    // The buffer, Stripe fee and floor are admin-editable (PricingSettings). Rounding stays fixed.
    // static readonly, not const: a decimal const is inlined at its use site, leaving the generated
    // type initialiser that sets the backing field permanently unexecuted.
    private static readonly decimal RoundingIncrement = 0.50m;   // round suggestions up to the nearest 50p

    public async Task<PriceRecommendation> RecommendAsync(
        int competitionId,
        int numberOfRounds,
        DateTime startDateUtc,
        int? seasonId,
        CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(competitionId);
        Guard.Against.NegativeOrZero(numberOfRounds);
        Guard.Against.Default(startDateUtc);

        var utcNow = dateTimeProvider.UtcNow;

        var settings = await GetPricingSettingsAsync(cancellationToken);
        var stripeFee = await GetStripeFeeAsync(cancellationToken);
        var annualRunningCost = await GetAnnualRunningCostAsync(cancellationToken);

        var otherPaidRounds = await GetOtherPaidRoundsInHorizonAsync(startDateUtc, seasonId, cancellationToken);
        var expectedPlayers = await GetLastComparableSeasonPlayerCountAsync(competitionId, seasonId, utcNow, cancellationToken);

        return PriceRecommendationCalculator.Recommend(
            annualRunningCost: annualRunningCost,
            seasonRounds: numberOfRounds,
            totalPaidRoundsInHorizon: numberOfRounds + otherPaidRounds,
            expectedPlayers: expectedPlayers,
            bufferRate: settings.BufferRate,
            stripePercent: stripeFee.PercentFee,
            stripeFixedFee: stripeFee.FixedFee,
            minimumFloor: settings.MinimumFloor,
            roundingIncrement: RoundingIncrement);
    }

    /// <summary>The saved settings, or the built-in defaults if none have been saved.</summary>
    private async Task<DomainPricingSettings> GetPricingSettingsAsync(CancellationToken cancellationToken)
    {
        var rows = await pricingSettingsQuery.ExecuteAsync(cancellationToken);
        var live = LivePricingSettings.From(rows);

        if (live is null)
            return DomainPricingSettings.CreateDefault();

        return new DomainPricingSettings(live.Id, live.BufferRate, live.MinimumFloor);
    }

    /// <summary>
    /// What Stripe charges, or the built-in default if nobody has recorded it.
    /// </summary>
    /// <remarks>
    /// Picking Stripe out of the fees is a rule: these are the fees for taking a payment, and Stripe is who takes it. The
    /// text-message and email providers in the same table are costs of a different kind and belong to other calculations.
    /// </remarks>
    private async Task<ServiceFee> GetStripeFeeAsync(CancellationToken cancellationToken)
    {
        var fees = await serviceFeesQuery.ExecuteAsync(cancellationToken);
        var stripe = fees.SingleOrDefault(fee => fee.Provider == nameof(ServiceFeeProvider.Stripe));

        if (stripe is null)
            return ServiceFee.CreateDefault(ServiceFeeProvider.Stripe);

        return new ServiceFee(0, ServiceFeeProvider.Stripe, stripe.PercentFee, stripe.FixedFee);
    }

    /// <summary>
    /// Every recorded cost, put on the same annual footing.
    /// </summary>
    /// <remarks>
    /// A cost whose frequency is not one the domain knows is left out rather than counted as an annual amount, because
    /// guessing at it would quietly change the price the administrator is shown.
    /// </remarks>
    private async Task<decimal> GetAnnualRunningCostAsync(CancellationToken cancellationToken)
    {
        var costs = await runningCostsQuery.ExecuteAsync(cancellationToken);

        return costs
            .Select(cost => Enum.TryParse<CostFrequency>(cost.Frequency, out var frequency)
                ? RunningCost.Annualise(cost.Amount, frequency)
                : 0m)
            .Sum();
    }

    /// <summary>
    /// How many rounds of other paid seasons run alongside this one.
    /// </summary>
    /// <remarks>
    /// Annual costs are shared across the paid seasons running during the twelve months from this season's start, weighted by
    /// length. A season counts when it overlaps that window - starting before the window ends and ending after it begins -
    /// and free seasons do not count at all, because they contribute nothing to cover the costs. This season itself is
    /// excluded, since its own rounds are added separately.
    /// </remarks>
    private async Task<int> GetOtherPaidRoundsInHorizonAsync(DateTime startDateUtc, int? seasonId, CancellationToken cancellationToken)
    {
        var seasons = await seasonPricingQuery.GetSeasonsAsync(cancellationToken);
        var horizonEnd = startDateUtc.AddYears(1);

        return seasons
            .Where(season => season.StandardPrice is not null
                             && season.Id != seasonId
                             && season.StartDateUtc < horizonEnd
                             && season.EndDateUtc > startDateUtc)
            .Sum(season => season.NumberOfRounds);
    }

    /// <summary>
    /// How many players to expect, taken from the most recently finished season of the same competition.
    /// </summary>
    /// <remarks>
    /// The break-even denominator. "Most recently finished" is by end date against the injected clock - the statement this
    /// replaces read the database's clock inline, which is also why it could not be tested. Nothing at all when there is no
    /// comparable season, which is what makes the calculator return a blank, explained recommendation rather than a number
    /// derived from no evidence.
    /// </remarks>
    private async Task<int?> GetLastComparableSeasonPlayerCountAsync(
        int competitionId,
        int? seasonId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var seasons = await seasonPricingQuery.GetSeasonsAsync(cancellationToken);

        var comparable = seasons
            .Where(season => season.CompetitionId == competitionId
                             && season.Id != seasonId
                             && season.EndDateUtc < utcNow)
            .MaxBy(season => season.EndDateUtc);

        if (comparable is null)
            return null;

        // The count is handed over as it is, including zero. The calculator already treats "nobody" and "no comparable
        // season" the same way and says so in its contract; repeating that decision here would be two places to change it.
        return await seasonPricingQuery.CountApprovedParticipantsAsync(comparable.Id, cancellationToken);
    }
}
