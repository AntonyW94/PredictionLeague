using FluentAssertions;
using ThePredictions.Domain.Models;
using Xunit;

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

    // LeagueStatsRepository recovers a member's boost multiplier for a round as
    // BoostedPoints / BasePoints, so it can apply the same boost to a subtotal of that round (the
    // finished matches only) without restating the boost rule in SQL. That is what keeps the round
    // change arrow honest: its baseline and its current value then differ by which matches count and
    // nothing else. It only holds while every boost is a whole-number multiplier of BasePoints.
    //
    // If this fails because a boost was added that adds points, overrides them, or scales them
    // fractionally, the SQL in LeagueStatsRepository.RecomputeAsync needs revisiting at the same time -
    // it will not fail on its own, it will quietly produce a wrong arrow.
    [Theory]
    [InlineData("DoubleUp")]
    [InlineData("Unknown")]
    [InlineData("")]
    public void ApplyBoost_ShouldProduceAnExactMultipleOfBasePoints_ForEveryBoostCode(string boostCode)
    {
        // Arrange - a base total that would expose fractional or additive scaling
        var result = CreateResult(basePoints: 7);

        // Act
        result.ApplyBoost(boostCode);

        // Assert
        result.BoostedPoints.Should().BeGreaterThanOrEqualTo(7,
            "a boost may increase points but must never reduce them");
        (result.BoostedPoints % 7).Should().Be(0,
            "the boosted total must be a whole-number multiple of the base total, or the multiplier " +
            "cannot be recovered as BoostedPoints / BasePoints");
    }

    [Fact]
    public void ApplyBoost_ShouldScaleASubtotalConsistently_WhenTheMultiplierIsRecoveredFromTheTotals()
    {
        // Arrange - the round scored 20 base points in total, of which 12 came from finished matches.
        // This mirrors what LeagueStatsRepository does mid-round: recover the multiplier from the round
        // totals, then apply it to the finished-match subtotal.
        var roundTotal = CreateResult(basePoints: 20);
        roundTotal.ApplyBoost("DoubleUp");

        const int finishedMatchesSubtotal = 12;

        // Act - SUM(subtotal) * MAX(boosted) / MAX(base), multiplying before dividing as the SQL does
        var boostedSubtotal = finishedMatchesSubtotal * roundTotal.BoostedPoints / roundTotal.BasePoints;

        // Assert - the subtotal is boosted by exactly the same factor as the round total, so a member
        // whose live matches have scored nothing sits at the same rank on both measures and shows no
        // change arrow
        boostedSubtotal.Should().Be(24);
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
