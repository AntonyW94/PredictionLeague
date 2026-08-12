using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Pricing;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Queries;

/// <summary>
/// The suggested Season Pass price for a season being drafted, with the workings behind it.
/// </summary>
/// <remarks>
/// The whole breakdown is shown on screen so an administrator can see how the number was arrived at and argue with it. That
/// makes carrying every field across worth asserting: a figure landing in the wrong row reads as a different recommendation
/// rather than as a bug.
/// </remarks>
public class GetSeasonPriceRecommendationQueryHandlerTests
{
    private static readonly DateTime StartDateUtc = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonPriceRecommendationService _recommendationService =
        Substitute.For<ISeasonPriceRecommendationService>();

    private readonly GetSeasonPriceRecommendationQueryHandler _handler;

    public GetSeasonPriceRecommendationQueryHandlerTests()
    {
        _handler = new GetSeasonPriceRecommendationQueryHandler(_recommendationService);
    }

    [Fact]
    public async Task Handle_ShouldReturnThePriceAndEveryFigureBehindIt()
    {
        // Arrange - every value distinct, so a field landing in the wrong place cannot pass.
        GivenRecommendation(new PriceRecommendation(
            suggestedStandardPrice: 12.50m,
            unavailableReason: null,
            annualRunningCost: 480m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 76,
            weight: 0.5m,
            apportionedCost: 240m,
            bufferRate: 0.2m,
            targetWithBuffer: 288m,
            expectedPlayers: 24,
            perPlayer: 12m,
            feeGrossedUp: 12.45m,
            floorApplied: false));

        // Act
        var recommendation = await HandleAsync();

        // Assert
        recommendation.SuggestedStandardPrice.Should().Be(12.50m);
        recommendation.UnavailableReason.Should().BeNull();
        recommendation.AnnualRunningCost.Should().Be(480m);
        recommendation.SeasonRounds.Should().Be(38);
        recommendation.TotalPaidRoundsInHorizon.Should().Be(76);
        recommendation.Weight.Should().Be(0.5m);
        recommendation.ApportionedCost.Should().Be(240m);
        recommendation.BufferRate.Should().Be(0.2m);
        recommendation.TargetWithBuffer.Should().Be(288m);
        recommendation.ExpectedPlayers.Should().Be(24);
        recommendation.PerPlayer.Should().Be(12m);
        recommendation.FeeGrossedUp.Should().Be(12.45m);
        recommendation.FloorApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldExplainWhyThereIsNoPrice_WhenNoneCanBeWorkedOut()
    {
        // Arrange - with no comparable season there is nothing to divide the cost by, and the screen says so.
        GivenRecommendation(new PriceRecommendation(
            suggestedStandardPrice: null,
            unavailableReason: "No comparable prior season",
            annualRunningCost: 480m,
            seasonRounds: 38,
            totalPaidRoundsInHorizon: 38,
            weight: 1m,
            apportionedCost: 480m,
            bufferRate: 0.2m,
            targetWithBuffer: 576m,
            expectedPlayers: null,
            perPlayer: null,
            feeGrossedUp: null,
            floorApplied: false));

        // Act
        var recommendation = await HandleAsync();

        // Assert
        recommendation.SuggestedStandardPrice.Should().BeNull();
        recommendation.UnavailableReason.Should().Be("No comparable prior season");
        recommendation.ExpectedPlayers.Should().BeNull();
        recommendation.PerPlayer.Should().BeNull();
        recommendation.FeeGrossedUp.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportThatTheMinimumPriceWasApplied()
    {
        // Arrange - the flag changes the wording on screen, so it has to survive the trip.
        GivenRecommendation(new PriceRecommendation(
            suggestedStandardPrice: 5m, unavailableReason: null, annualRunningCost: 100m, seasonRounds: 38,
            totalPaidRoundsInHorizon: 38, weight: 1m, apportionedCost: 100m, bufferRate: 0.2m,
            targetWithBuffer: 120m, expectedPlayers: 100, perPlayer: 1.20m, feeGrossedUp: 1.55m,
            floorApplied: true));

        // Act
        var recommendation = await HandleAsync();

        // Assert
        recommendation.FloorApplied.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldAskAboutTheSeasonBeingDrafted()
    {
        // Arrange - the season being edited is excluded from its own comparison, so its id has to be passed on.
        GivenRecommendation(new PriceRecommendation(
            suggestedStandardPrice: 10m, unavailableReason: null, annualRunningCost: 100m, seasonRounds: 38,
            totalPaidRoundsInHorizon: 38, weight: 1m, apportionedCost: 100m, bufferRate: 0.2m,
            targetWithBuffer: 120m, expectedPlayers: 12, perPlayer: 10m, feeGrossedUp: 10.5m,
            floorApplied: false));

        // Act
        await HandleAsync();

        // Assert
        await _recommendationService.Received(1).RecommendAsync(
            3, 38, StartDateUtc, 7, Arg.Any<CancellationToken>());
    }

    private void GivenRecommendation(PriceRecommendation recommendation) =>
        _recommendationService
            .RecommendAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(recommendation);

    private Task<Contracts.Admin.Seasons.PriceRecommendationDto> HandleAsync() =>
        _handler.Handle(
            new GetSeasonPriceRecommendationQuery(CompetitionId: 3, NumberOfRounds: 38, StartDateUtc, SeasonId: 7),
            CancellationToken.None);
}
