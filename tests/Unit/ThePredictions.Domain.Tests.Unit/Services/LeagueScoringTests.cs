using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// What a round is worth to a player in a league. The scoring rule of the whole game, and until now it existed only inside
/// the <c>MERGE</c> that rebuilt every league's round results - with no C# copy anywhere, which is why nothing tested it.
/// </summary>
public class LeagueScoringTests
{
    [Fact]
    public void BasePoints_ShouldPayForExactScoresAndCorrectResults()
    {
        // Arrange - every number distinct, so an arithmetic slip cannot land on the right total by luck.
        var counts = new OutcomeCounts(ExactScoreCount: 2, CorrectResultCount: 3, IncorrectCount: 4);

        // Act
        var points = LeagueScoring.BasePoints(counts, pointsForExactScore: 5, pointsForCorrectResult: 1);

        // Assert - two at five, three at one.
        points.Should().Be(13);
    }

    [Fact]
    public void BasePoints_ShouldPayNothingForAMiss()
    {
        // A wrong prediction is worth nothing, which is why the incorrect count is not in the sum. Four misses here.
        var counts = new OutcomeCounts(ExactScoreCount: 0, CorrectResultCount: 0, IncorrectCount: 4);

        // Act
        var points = LeagueScoring.BasePoints(counts, pointsForExactScore: 3, pointsForCorrectResult: 1);

        // Assert
        points.Should().Be(0);
    }

    [Fact]
    public void BasePoints_ShouldPayNothing_ForARoundNobodyHasScoredIn()
    {
        // Act
        var points = LeagueScoring.BasePoints(new OutcomeCounts(0, 0, 0), 3, 1);

        // Assert
        points.Should().Be(0);
    }

    /// <summary>
    /// The product feature this arithmetic implements: each league sets its own points, so the same predictions are worth
    /// different totals in two leagues watching the same fixtures.
    /// </summary>
    [Fact]
    public void BasePoints_ShouldFollowTheLeaguesOwnSettings()
    {
        // Arrange
        var counts = new OutcomeCounts(ExactScoreCount: 2, CorrectResultCount: 3, IncorrectCount: 0);

        // Act
        var generous = LeagueScoring.BasePoints(counts, pointsForExactScore: 10, pointsForCorrectResult: 5);
        var stingy = LeagueScoring.BasePoints(counts, pointsForExactScore: 3, pointsForCorrectResult: 1);

        // Assert
        generous.Should().Be(35);
        stingy.Should().Be(9);
    }

    [Fact]
    public void BasePoints_ShouldNotSwapTheTwoRates()
    {
        // Arrange - an exact score is worth more than a correct result in every league on the site, and a transposition
        // would be invisible with equal counts. Three exact scores, one correct result.
        var counts = new OutcomeCounts(ExactScoreCount: 3, CorrectResultCount: 1, IncorrectCount: 0);

        // Act
        var points = LeagueScoring.BasePoints(counts, pointsForExactScore: 3, pointsForCorrectResult: 1);

        // Assert - ten, not six.
        points.Should().Be(10);
    }
}
