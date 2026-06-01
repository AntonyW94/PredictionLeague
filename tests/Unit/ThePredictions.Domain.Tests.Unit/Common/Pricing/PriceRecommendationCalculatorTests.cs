using FluentAssertions;
using ThePredictions.Domain.Common.Pricing;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common.Pricing;

public class PriceRecommendationCalculatorTests
{
    // Default fee/buffer assumptions used by the worked examples.
    private const decimal Buffer = 0.15m;
    private const decimal StripePercent = 0.015m;
    private const decimal StripeFixed = 0.20m;
    private const decimal Floor = 1.00m;
    private const decimal Rounding = 0.50m;

    [Fact]
    public void Recommend_ShouldComputeSuggestion_ForSinglePaidSeason()
    {
        // Arrange: £240/yr running costs, only paid season (weight 1), 20 expected players.
        // target = 240 * 1.15 = 276; perPlayer = 13.80; grossed = 14.00 / 0.985 = 14.213...;
        // ceil to nearest 0.50 => 14.50.

        // Act
        var result = PriceRecommendationCalculator.Recommend(
            annualRunningCost:240m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 38,
            expectedPlayers: 20,
            bufferRate: Buffer,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixed,
            minimumFloor: Floor,
            roundingIncrement: Rounding);

        // Assert
        result.Weight.Should().Be(1m);
        result.ApportionedCost.Should().Be(240m);
        result.TargetWithBuffer.Should().Be(276m);
        result.PerPlayer.Should().Be(13.80m);
        result.FeeGrossedUp.Should().BeApproximately(14.2132m, 0.0001m);
        result.FloorApplied.Should().BeFalse();
        result.SuggestedStandardPrice.Should().Be(14.50m);
        result.UnavailableReason.Should().BeNull();
    }

    [Fact]
    public void Recommend_ShouldWeightLongerSeasonsMore_WhenHorizonHasMultiplePaidSeasons()
    {
        // Arrange: 38-round season sharing a horizon of 52 rounds (a 14-round cup also paid).
        // weight = 38/52 = 0.7308; apportioned = 520 * 0.7308 = 380; target = 437; 50 players.

        // Act
        var result = PriceRecommendationCalculator.Recommend(
            annualRunningCost:520m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 52,
            expectedPlayers: 50,
            bufferRate: Buffer,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixed,
            minimumFloor: Floor,
            roundingIncrement: Rounding);

        // Assert
        result.Weight.Should().BeApproximately(0.7308m, 0.0001m);
        result.ApportionedCost.Should().BeApproximately(380m, 0.01m);
        result.TargetWithBuffer.Should().BeApproximately(437m, 0.01m);
        result.SuggestedStandardPrice.Should().Be(9.50m);
        result.FloorApplied.Should().BeFalse();
    }

    [Fact]
    public void Recommend_ShouldApplyFloor_WhenComputedPriceIsBelowFloor()
    {
        // Arrange: no running costs yet => computed price is just the grossed-up fixed fee (~0.20),
        // so the floor lifts it to 1.00.

        // Act
        var result = PriceRecommendationCalculator.Recommend(
            annualRunningCost:0m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 38,
            expectedPlayers: 10,
            bufferRate: Buffer,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixed,
            minimumFloor: Floor,
            roundingIncrement: Rounding);

        // Assert
        result.FloorApplied.Should().BeTrue();
        result.SuggestedStandardPrice.Should().Be(1.00m);
    }

    [Fact]
    public void Recommend_ShouldReturnBlankWithReason_WhenNoExpectedPlayers()
    {
        // Act
        var result = PriceRecommendationCalculator.Recommend(
            annualRunningCost:240m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 38,
            expectedPlayers: null,
            bufferRate: Buffer,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixed,
            minimumFloor: Floor,
            roundingIncrement: Rounding);

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
        result.ExpectedPlayers.Should().BeNull();
        result.PerPlayer.Should().BeNull();
        result.FeeGrossedUp.Should().BeNull();
        result.UnavailableReason.Should().Be(PriceRecommendationCalculator.NoComparableSeasonReason);
        // Cost breakdown is still populated for transparency.
        result.TargetWithBuffer.Should().Be(276m);
    }

    [Fact]
    public void Recommend_ShouldReturnBlank_WhenExpectedPlayersZeroOrNegative()
    {
        // Act
        var result = PriceRecommendationCalculator.Recommend(
            annualRunningCost:240m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 38,
            expectedPlayers: 0,
            bufferRate: Buffer,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixed,
            minimumFloor: Floor,
            roundingIncrement: Rounding);

        // Assert
        result.SuggestedStandardPrice.Should().BeNull();
        result.UnavailableReason.Should().Be(PriceRecommendationCalculator.NoComparableSeasonReason);
    }

    [Theory]
    [InlineData(-1, 38, 38)]      // negative cost
    [InlineData(240, 0, 38)]      // zero rounds
    [InlineData(240, 38, 37)]     // horizon smaller than this season's rounds
    public void Recommend_ShouldThrow_WhenInputsAreInvalid(decimal cost, int rounds, int horizon)
    {
        // Act
        var act = () => PriceRecommendationCalculator.Recommend(
            annualRunningCost:cost,
            seasonRounds: rounds,
            totalPaidRoundsInHorizon: horizon,
            expectedPlayers: 20,
            bufferRate: Buffer,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixed,
            minimumFloor: Floor,
            roundingIncrement: Rounding);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1, 0.015, 0.20, 1.00, 0.50)]   // negative buffer
    [InlineData(0.15, 1.0, 0.20, 1.00, 0.50)]     // stripe percent out of range
    [InlineData(0.15, 0.015, -0.1, 1.00, 0.50)]   // negative fixed fee
    [InlineData(0.15, 0.015, 0.20, -0.1, 0.50)]   // negative floor
    [InlineData(0.15, 0.015, 0.20, 1.00, 0.00)]   // zero rounding increment
    public void Recommend_ShouldThrow_WhenRatesAreInvalid(decimal buffer, decimal stripePercent, decimal stripeFixed, decimal floor, decimal rounding)
    {
        // Act
        var act = () => PriceRecommendationCalculator.Recommend(
            annualRunningCost:240m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 38,
            expectedPlayers: 20,
            bufferRate: buffer,
            stripePercent: stripePercent,
            stripeFixedFee: stripeFixed,
            minimumFloor: floor,
            roundingIncrement: rounding);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
