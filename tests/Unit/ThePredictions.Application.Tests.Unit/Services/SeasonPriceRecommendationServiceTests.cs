using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

/// <summary>
/// Suggests what to charge for a season pass. It gathers the running costs, the payment provider's fee and how many players
/// the last comparable season drew, then hands them to the domain calculator - so what matters here is that it gathers the
/// right numbers and copes when a setting has never been saved.
/// </summary>
/// <remarks>
/// Three of its five reads turned out to be the administrator's own reads, so they arrive through those ports. The two that
/// are its own carry the rules that were previously inside SQL: which seasons share the annual costs, and which finished
/// season is the comparable one - the latter measured against the injected clock rather than one read inline in the
/// statement's parameters, which is why it could not be tested before.
/// </remarks>
public class SeasonPriceRecommendationServiceTests
{
    private const int CompetitionId = 3;
    private const int SeasonId = 11;
    private const int NumberOfRounds = 38;

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime StartDateUtc = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly IPricingSettingsQuery _pricingSettings = Substitute.For<IPricingSettingsQuery>();
    private readonly IServiceFeesQuery _serviceFees = Substitute.For<IServiceFeesQuery>();
    private readonly IRunningCostsQuery _runningCosts = Substitute.For<IRunningCostsQuery>();
    private readonly ISeasonPricingQuery _seasonPricing = Substitute.For<ISeasonPricingQuery>();

    public SeasonPriceRecommendationServiceTests()
    {
        GivenPricingSettings();
        GivenStripeFee();
        GivenRunningCosts();
        GivenSeasons();
        GivenParticipantCount(0);
    }

    #region Guards

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

    #endregion

    #region Falling back to defaults

    [Fact]
    public async Task RecommendAsync_ShouldFallBackToDefaultSettings_WhenNoneHaveBeenSaved()
    {
        // A fresh environment has no settings and no fees recorded; the recommendation still has to work.
        GivenComparableSeasonWith(playerCount: 20);

        var act = () => RecommendAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecommendAsync_ShouldUseTheSavedSettingsWhenThereAreSome()
    {
        // Arrange
        GivenPricingSettings(new PricingSettingsRow(1, 0.15m, 5m));
        GivenStripeFee(new ServiceFeeRow(nameof(ServiceFeeProvider.Stripe), 0.015m, 0.20m));
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));
        GivenComparableSeasonWith(playerCount: 20);

        // Act
        var result = await RecommendAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RecommendAsync_ShouldTreatTheEarliestSavedRowAsTheLiveSettings()
    {
        // The same rule the administrator's screen applies, from the same place. The rows deliberately arrive out of order.
        GivenPricingSettings(new PricingSettingsRow(9, 0.99m, 99m), new PricingSettingsRow(2, 0m, 0m));
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));
        GivenComparableSeasonWith(playerCount: 20);

        // Act
        var result = await RecommendAsync();

        // Assert - the 99% buffer of the later row would put the price far higher than this.
        result.SuggestedStandardPrice!.Value.Should().BeLessThan(50m);
    }

    [Fact]
    public async Task RecommendAsync_ShouldIgnoreAnotherProvidersFee()
    {
        // These are the fees for taking a payment, and Stripe is who takes it. The messaging providers in the same table
        // belong to other calculations, and a ninety-percent fee would wreck the price if one were picked up by mistake.
        GivenComparableSeasonWith(playerCount: 20);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        GivenStripeFee();
        var withNoFeesRecorded = await RecommendAsync();

        GivenStripeFee(new ServiceFeeRow("Sms", 0.90m, 5m));
        var withOnlyAMessagingFee = await RecommendAsync();

        // Assert - the same answer either way, because both fall back to the built-in payment fee.
        withOnlyAMessagingFee.SuggestedStandardPrice.Should().Be(withNoFeesRecorded.SuggestedStandardPrice);
    }

    #endregion

    #region What it reports

    [Fact]
    public async Task RecommendAsync_ShouldReturnABlankRecommendation_WhenThereIsNoComparableSeason()
    {
        // Nothing to divide the costs by, so it says so rather than inventing a price.
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
        result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RecommendAsync_ShouldReturnABlankRecommendation_WhenTheComparableSeasonHadNobodyInIt()
    {
        // Arrange - a finished season exists, but nobody was ever approved into a league in it. There is still nothing to
        // divide the costs by, and the calculator treats that the same as having no comparable season at all.
        GivenSeasons(FinishedSeason());
        GivenParticipantCount(0);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
        result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RecommendAsync_ShouldSuggestAPrice_WhenTheLastSeasonHadPlayers()
    {
        // Arrange
        GivenComparableSeasonWith(playerCount: 20);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        result.SuggestedStandardPrice.Should().NotBeNull();
        result.SuggestedStandardPrice!.Value.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task RecommendAsync_ShouldRoundTheSuggestionToTheNearestFiftyPence()
    {
        // Arrange
        GivenComparableSeasonWith(playerCount: 17);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 397m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        (result.SuggestedStandardPrice!.Value % 0.50m).Should().Be(0m);
    }

    #endregion

    #region Gathering the costs

    [Fact]
    public async Task RecommendAsync_ShouldAnnualiseAMonthlyCost()
    {
        // A monthly cost counts twelve times over the year, so it should push the price above the same figure charged once.
        GivenComparableSeasonWith(playerCount: 20);

        GivenRunningCosts(new RunningCostRow(1, "Hosting", 20m, nameof(CostFrequency.Monthly), StartDateUtc, null, null));
        var monthly = await RecommendAsync();

        GivenRunningCosts(new RunningCostRow(1, "Hosting", 20m, nameof(CostFrequency.Annual), StartDateUtc, null, null));
        var annual = await RecommendAsync();

        monthly.SuggestedStandardPrice.Should().BeGreaterThan(annual.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldAddEveryRecordedCostTogether()
    {
        // Arrange
        GivenComparableSeasonWith(playerCount: 20);

        GivenRunningCosts(new RunningCostRow(1, "Hosting", 100m, nameof(CostFrequency.Annual), StartDateUtc, null, null));
        var one = await RecommendAsync();

        GivenRunningCosts(
            new RunningCostRow(1, "Hosting", 100m, nameof(CostFrequency.Annual), StartDateUtc, null, null),
            new RunningCostRow(2, "Setup", 100m, nameof(CostFrequency.OneOff), StartDateUtc, null, null));
        var two = await RecommendAsync();

        two.SuggestedStandardPrice.Should().BeGreaterThan(one.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldLeaveOutACostWhoseFrequencyIsNotRecognised()
    {
        // Guessing at it would quietly change the price an administrator is shown.
        GivenComparableSeasonWith(playerCount: 20);

        GivenRunningCosts(new RunningCostRow(1, "Hosting", 100m, nameof(CostFrequency.Annual), StartDateUtc, null, null));
        var known = await RecommendAsync();

        GivenRunningCosts(
            new RunningCostRow(1, "Hosting", 100m, nameof(CostFrequency.Annual), StartDateUtc, null, null),
            new RunningCostRow(2, "Mystery", 9999m, "Fortnightly", StartDateUtc, null, null));
        var withMystery = await RecommendAsync();

        withMystery.SuggestedStandardPrice.Should().Be(known.SuggestedStandardPrice!.Value);
    }

    #endregion

    #region Sharing the costs across seasons

    [Fact]
    public async Task RecommendAsync_ShouldShareTheCostsWithAnotherPaidSeasonRunningAlongside()
    {
        // Two paid competitions in the same year split the annual bill, so each should ask less.
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 480m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        GivenSeasons(FinishedSeason());
        GivenParticipantCount(20);
        var alone = await RecommendAsync();

        GivenSeasons(FinishedSeason(), OverlappingPaidSeason());
        var shared = await RecommendAsync();

        shared.SuggestedStandardPrice.Should().BeLessThan(alone.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldNotShareTheCostsWithAFreeSeason()
    {
        // A free season brings in nothing towards the bill, so it does not help pay it.
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 480m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        GivenSeasons(FinishedSeason());
        GivenParticipantCount(20);
        var alone = await RecommendAsync();

        GivenSeasons(FinishedSeason(), OverlappingPaidSeason() with { StandardPrice = null });
        var withFree = await RecommendAsync();

        withFree.SuggestedStandardPrice.Should().Be(alone.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldNotShareTheCostsWithASeasonOutsideTheYearAhead()
    {
        // Arrange
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 480m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        GivenSeasons(FinishedSeason());
        GivenParticipantCount(20);
        var alone = await RecommendAsync();

        GivenSeasons(
            FinishedSeason(),
            OverlappingPaidSeason() with
            {
                StartDateUtc = StartDateUtc.AddYears(2),
                EndDateUtc = StartDateUtc.AddYears(3)
            });
        var withDistant = await RecommendAsync();

        withDistant.SuggestedStandardPrice.Should().Be(alone.SuggestedStandardPrice!.Value);
    }

    [Fact]
    public async Task RecommendAsync_ShouldNotShareTheCostsWithTheSeasonBeingPriced()
    {
        // Its own rounds are added separately; counting them twice would halve the price.
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 480m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        GivenSeasons(FinishedSeason());
        GivenParticipantCount(20);
        var alone = await RecommendAsync();

        GivenSeasons(FinishedSeason(), OverlappingPaidSeason() with { Id = SeasonId });
        var withItself = await RecommendAsync();

        withItself.SuggestedStandardPrice.Should().Be(alone.SuggestedStandardPrice!.Value);
    }

    #endregion

    #region Choosing the comparable season

    [Fact]
    public async Task RecommendAsync_ShouldCompareAgainstTheMostRecentlyFinishedSeason()
    {
        // Arrange - two finished seasons of the same competition, and the newer one is the comparison.
        GivenSeasons(
            FinishedSeason() with { Id = 100, EndDateUtc = Now.AddYears(-3) },
            FinishedSeason() with { Id = 200, EndDateUtc = Now.AddDays(-1) });
        GivenParticipantCount(20);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        await RecommendAsync();

        // Assert
        await _seasonPricing.Received(1).CountApprovedParticipantsAsync(200, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecommendAsync_ShouldNotCompareAgainstASeasonThatHasNotFinished()
    {
        // Measured against the injected clock. The statement this replaces read the clock inline, which is why this could
        // never be tested. The player count is non-zero so a mistake here would show as a price rather than a blank.
        GivenSeasons(FinishedSeason() with { Id = 100, EndDateUtc = Now.AddDays(1) });
        GivenParticipantCount(20);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
        await _seasonPricing.DidNotReceiveWithAnyArgs().CountApprovedParticipantsAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task RecommendAsync_ShouldNotCompareAgainstAnotherCompetitionsSeason()
    {
        // Arrange - a finished season with players in it, but of a different competition. The player count is deliberately
        // non-zero, so picking this season up by mistake would produce a price rather than a blank.
        GivenSeasons(FinishedSeason() with { Id = 100, CompetitionId = CompetitionId + 1 });
        GivenParticipantCount(20);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
        await _seasonPricing.DidNotReceiveWithAnyArgs().CountApprovedParticipantsAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task RecommendAsync_ShouldNotCompareTheSeasonBeingPricedAgainstItself()
    {
        // Arrange - the only finished season of this competition is the one being priced, and it has players in it.
        GivenSeasons(FinishedSeason() with { Id = SeasonId });
        GivenParticipantCount(20);
        GivenRunningCosts(new RunningCostRow(1, "Hosting", 240m, nameof(CostFrequency.Annual), StartDateUtc, null, null));

        // Act
        var result = await RecommendAsync();

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
    }

    [Fact]
    public async Task RecommendAsync_ShouldCopeWithANewSeasonThatHasNoIdYet()
    {
        // Arrange
        GivenComparableSeasonWith(playerCount: 20);

        // Act
        var act = () => RecommendAsync(seasonId: null);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    private SeasonPriceRecommendationService BuildService() =>
        new(_pricingSettings, _serviceFees, _runningCosts, _seasonPricing, new TestDateTimeProvider(Now));

    private Task<Domain.Common.Pricing.PriceRecommendation> RecommendAsync(
        int competitionId = CompetitionId,
        int numberOfRounds = NumberOfRounds,
        DateTime? startDateUtc = null,
        int? seasonId = SeasonId) =>
        BuildService().RecommendAsync(competitionId, numberOfRounds, startDateUtc ?? StartDateUtc, seasonId, CancellationToken.None);

    private void GivenPricingSettings(params PricingSettingsRow[] rows) =>
        _pricingSettings.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(rows);

    private void GivenStripeFee(params ServiceFeeRow[] fees) =>
        _serviceFees.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(fees);

    private void GivenRunningCosts(params RunningCostRow[] costs) =>
        _runningCosts.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(costs);

    private void GivenSeasons(params SeasonPricingRow[] seasons) =>
        _seasonPricing.GetSeasonsAsync(Arg.Any<CancellationToken>()).Returns(seasons);

    private void GivenParticipantCount(int count) =>
        _seasonPricing.CountApprovedParticipantsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(count);

    private void GivenComparableSeasonWith(int playerCount)
    {
        GivenSeasons(FinishedSeason());
        GivenParticipantCount(playerCount);
    }

    /// <summary>A finished season of the same competition, which is what makes it comparable.</summary>
    private static SeasonPricingRow FinishedSeason() =>
        new(Id: 100, CompetitionId, NumberOfRounds, Now.AddYears(-1), Now.AddDays(-30), StandardPrice: 10m);

    /// <summary>A paid season running inside the twelve months from the priced season's start.</summary>
    private static SeasonPricingRow OverlappingPaidSeason() =>
        new(Id: 300, CompetitionId + 5, NumberOfRounds, StartDateUtc.AddDays(1), StartDateUtc.AddMonths(6), StandardPrice: 10m);
}
