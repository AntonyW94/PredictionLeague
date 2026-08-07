using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

/// <summary>
/// Suggests what to charge for a season pass. It gathers the running costs, the payment provider's
/// fee and how many players the last comparable season drew, then hands them to the domain
/// calculator - so what matters here is that it gathers the right numbers and copes when a setting
/// has never been seeded.
/// </summary>
public class SeasonPriceRecommendationServiceTests
{
    private const int CompetitionId = 3;
    private const int SeasonId = 11;
    private const int NumberOfRounds = 38;

    private static readonly DateTime StartDateUtc = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _db = Substitute.For<IApplicationReadDbConnection>();

    private SeasonPriceRecommendationService BuildService() => new(_db);

    private Task<Domain.Common.Pricing.PriceRecommendation> RecommendAsync(
        int competitionId = CompetitionId,
        int numberOfRounds = NumberOfRounds,
        DateTime? startDateUtc = null,
        int? seasonId = SeasonId) =>
        BuildService().RecommendAsync(competitionId, numberOfRounds, startDateUtc ?? StartDateUtc, seasonId, CancellationToken.None);

    private void GivenPricingSettings(PricingSettings? settings) =>
        _db.QuerySingleOrDefaultAsync<PricingSettings>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(settings);

    private void GivenStripeFee(ServiceFee? fee) =>
        _db.QuerySingleOrDefaultAsync<ServiceFee>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(fee);

    private void GivenRunningCosts(params (decimal Amount, CostFrequency Frequency)[] costs) =>
        _db.QueryAsync<SeasonPriceRecommendationService.CostRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(costs.Select(c => new SeasonPriceRecommendationService.CostRow
            {
                Amount = c.Amount,
                Frequency = c.Frequency.ToString()
            }).ToList());

    /// <summary>Both the horizon round count and the player count come back as ints.</summary>
    private void GivenIntScalars(int otherPaidRounds, int playerCount)
    {
        var call = 0;
        _db.QuerySingleOrDefaultAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(_ => call++ == 0 ? otherPaidRounds : playerCount);
    }

    // ---------- guards ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RecommendAsync_ShouldRejectAnInvalidCompetition(int competitionId)
    {
        var act = () => RecommendAsync(competitionId: competitionId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RecommendAsync_ShouldRejectAnInvalidRoundCount(int numberOfRounds)
    {
        var act = () => RecommendAsync(numberOfRounds: numberOfRounds);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecommendAsync_ShouldRejectAMissingStartDate()
    {
        // Called directly: the helper's nullable parameter would turn `default` back into a real date.
        var act = () => BuildService().RecommendAsync(CompetitionId, NumberOfRounds, default, SeasonId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------- falling back to defaults ----------

    [Fact]
    public async Task RecommendAsync_ShouldFallBackToDefaultSettings_WhenNoneHaveBeenSeeded()
    {
        // A fresh environment has no PricingSettings row; the recommendation still has to work.
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);

        var act = () => RecommendAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecommendAsync_ShouldUseTheConfiguredSettingsWhenPresent()
    {
        GivenPricingSettings(PricingSettings.CreateDefault());
        GivenStripeFee(ServiceFee.CreateDefault(ServiceFeeProvider.Stripe));
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((240m, CostFrequency.Annual));

        var result = await RecommendAsync();

        result.Should().NotBeNull();
    }

    // ---------- what it reports ----------

    [Fact]
    public async Task RecommendAsync_ShouldReturnABlankRecommendation_WhenNoComparableSeasonExists()
    {
        // Nothing to divide the costs by, so it says so rather than inventing a price.
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 0);
        GivenRunningCosts((240m, CostFrequency.Annual));

        var result = await RecommendAsync();

        result.SuggestedStandardPrice.Should().BeNull();
        result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RecommendAsync_ShouldSuggestAPrice_WhenTheLastSeasonHadPlayers()
    {
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((240m, CostFrequency.Annual));

        var result = await RecommendAsync();

        result.SuggestedStandardPrice.Should().NotBeNull();
        result.SuggestedStandardPrice!.Value.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task RecommendAsync_ShouldRoundTheSuggestionToTheNearestFiftyPence()
    {
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 17);
        GivenRunningCosts((397m, CostFrequency.Annual));

        var result = await RecommendAsync();

        (result.SuggestedStandardPrice!.Value % 0.50m).Should().Be(0m);
    }

    // ---------- gathering the costs ----------

    [Fact]
    public async Task RecommendAsync_ShouldAnnualiseAMonthlyCost()
    {
        // A monthly cost counts twelve times over the year, so it should push the price above the
        // same figure charged once.
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((20m, CostFrequency.Monthly));
        var monthly = await RecommendAsync();

        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((20m, CostFrequency.Annual));
        var annual = await RecommendAsync();

        monthly.SuggestedStandardPrice.Should().BeGreaterThan(annual.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldAddEveryRecordedCostTogether()
    {
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((100m, CostFrequency.Annual));
        var one = await RecommendAsync();

        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((100m, CostFrequency.Annual), (100m, CostFrequency.OneOff));
        var two = await RecommendAsync();

        two.SuggestedStandardPrice.Should().BeGreaterThan(one.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldShareTheCostsWithOtherPaidSeasonsRunningAlongside()
    {
        // Two paid competitions in the same year split the annual bill, so each should ask less.
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((480m, CostFrequency.Annual));
        var alone = await RecommendAsync();

        GivenIntScalars(otherPaidRounds: 38, playerCount: 20);
        GivenRunningCosts((480m, CostFrequency.Annual));
        var shared = await RecommendAsync();

        shared.SuggestedStandardPrice.Should().BeLessThan(alone.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldExcludeTheSeasonBeingPricedFromItsOwnComparison()
    {
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((240m, CostFrequency.Annual));

        await RecommendAsync(seasonId: SeasonId);

        await _db.Received().QuerySingleOrDefaultAsync<int>(
            Arg.Any<string>(), Arg.Any<CancellationToken>(),
            Arg.Is<object?>(p => p != null && p.GetType().GetProperty("SeasonId")!.GetValue(p)!.Equals(SeasonId)));
    }

    [Fact]
    public async Task RecommendAsync_ShouldCopeWithANewSeasonThatHasNoIdYet()
    {
        GivenPricingSettings(null);
        GivenStripeFee(null);
        GivenIntScalars(otherPaidRounds: 0, playerCount: 20);
        GivenRunningCosts((240m, CostFrequency.Annual));

        var act = () => RecommendAsync(seasonId: null);

        await act.Should().NotThrowAsync();
    }
}
