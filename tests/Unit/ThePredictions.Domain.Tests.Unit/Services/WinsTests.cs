using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Who won each period of a league. This was four <c>RANK() OVER (PARTITION BY ...)</c> windows across two
/// handlers - the records tile asking who won the most, the season recap asking how many one player won.
/// </summary>
public class WinsTests
{
    private sealed record Score(int Period, string UserId, int Points);

    private static IReadOnlyList<string> WinnersOf(params Score[] scores) =>
        Wins.ByPeriod(scores, score => score.Period, score => score.UserId, score => score.Points);

    [Fact]
    public void ByPeriod_ShouldReturnTheHighestScorerInEachPeriod()
    {
        WinnersOf(
                new Score(1, "ada", 20), new Score(1, "grace", 10),
                new Score(2, "ada", 5), new Score(2, "grace", 30))
            .Should().Equal("ada", "grace");
    }

    [Fact]
    public void ByPeriod_ShouldReturnBothPlayers_WhenAPeriodIsDrawn()
    {
        // RANK rather than ROW_NUMBER: a shared win counts for both.
        WinnersOf(new Score(1, "ada", 20), new Score(1, "grace", 20))
            .Should().BeEquivalentTo(["ada", "grace"]);
    }

    [Fact]
    public void ByPeriod_ShouldReturnOneEntryPerWin()
    {
        // Three wins for the same player is three entries, so a caller can simply count.
        WinnersOf(
                new Score(1, "ada", 20),
                new Score(2, "ada", 20),
                new Score(3, "ada", 20))
            .Should().Equal("ada", "ada", "ada");
    }

    [Fact]
    public void ByPeriod_ShouldTotalPointsWithinAPeriod_BeforeDecidingTheWinner()
    {
        // A month is won on its total, not by winning its best round: Grace takes the single best round, Ada the month.
        Wins.ByPeriod(
                new[]
                {
                    new Score(1, "ada", 14), new Score(1, "grace", 15),
                    new Score(1, "ada", 14), new Score(1, "grace", 12)
                },
                score => score.Period,
                score => score.UserId,
                score => score.Points)
            .Should().Equal("ada");
    }

    [Fact]
    public void ByPeriod_ShouldReturnNobody_WhenAPeriodWasScorelessForEveryone()
    {
        // Otherwise a round created but not yet scored hands every member of the league a win.
        WinnersOf(new Score(1, "ada", 0), new Score(1, "grace", 0)).Should().BeEmpty();
    }

    [Fact]
    public void ByPeriod_ShouldStillReturnWinnersOfTheOtherPeriods_WhenOneWasScoreless()
    {
        WinnersOf(
                new Score(1, "ada", 0), new Score(1, "grace", 0),
                new Score(2, "ada", 7))
            .Should().Equal("ada");
    }

    [Fact]
    public void ByPeriod_ShouldReturnNobody_WhenThereAreNoScores()
    {
        WinnersOf().Should().BeEmpty();
    }
}
