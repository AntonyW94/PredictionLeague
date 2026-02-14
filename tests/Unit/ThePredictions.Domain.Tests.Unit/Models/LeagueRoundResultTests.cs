using FluentAssertions;
using PredictionLeague.Domain.Models;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeagueRoundResultTests
{
    private static LeagueRoundResult CreateResult(int basePoints = 10, string? appliedBoostCode = "DoubleUp")
    {
        return new LeagueRoundResult(
            leagueId: 1, roundId: 1, userId: "user-1",
            basePoints: basePoints, boostedPoints: basePoints,
            hasBoost: true, appliedBoostCode: appliedBoostCode, exactScoreCount: 0);
    }

    [Fact]
    public void ApplyBoost_ShouldDoubleBasePoints_WhenBoostCodeIsDoubleUp()
    {
        // Arrange
        var result = CreateResult(basePoints: 10);

        // Act
        result.ApplyBoost("DoubleUp");

        // Assert
        result.BoostedPoints.Should().Be(20);
    }

    [Fact]
    public void ApplyBoost_ShouldSetBoostedPointsToBasePoints_WhenBoostCodeIsUnrecognised()
    {
        // Arrange
        var result = CreateResult(basePoints: 10, appliedBoostCode: "Unknown");

        // Act
        result.ApplyBoost("Unknown");

        // Assert
        result.BoostedPoints.Should().Be(10);
    }

    [Fact]
    public void ApplyBoost_ShouldSetBoostedPointsToBasePoints_WhenBoostCodeIsEmpty()
    {
        // Arrange
        var result = CreateResult(basePoints: 10, appliedBoostCode: "");

        // Act
        result.ApplyBoost("");

        // Assert
        result.BoostedPoints.Should().Be(10);
    }

    [Fact]
    public void ApplyBoost_ShouldHandleZeroBasePoints_WhenDoubleUp()
    {
        // Arrange
        var result = CreateResult(basePoints: 0);

        // Act
        result.ApplyBoost("DoubleUp");

        // Assert
        result.BoostedPoints.Should().Be(0);
    }

    [Fact]
    public void ApplyBoost_ShouldBeCaseSensitive_WhenBoostCodeHasWrongCase()
    {
        // Arrange
        var result = CreateResult(basePoints: 10);

        // Act
        result.ApplyBoost("doubleup");

        // Assert — falls through to default (base points)
        result.BoostedPoints.Should().Be(10);
    }

    [Fact]
    public void ApplyBoost_ShouldOverwritePreviousBoostedPoints_WhenCalledAgain()
    {
        // Arrange
        var result = CreateResult(basePoints: 10);
        result.ApplyBoost("DoubleUp");
        result.BoostedPoints.Should().Be(20);

        // Act — call again with unrecognised code
        result.ApplyBoost("Unknown");

        // Assert — overwrites to base points
        result.BoostedPoints.Should().Be(10);
    }
}
