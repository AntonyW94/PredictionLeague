using FluentAssertions;
using ThePredictions.Application.Common.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Helpers;

/// <summary>
/// Splits a prize between joint winners. Real money, so the parts must add back up to exactly the
/// original amount - any odd penny goes to someone rather than disappearing.
/// </summary>
public class PrizeDistributionHelperTests
{
    [Fact]
    public void DistributePrizeMoney_ShouldReturnNothing_WhenThereAreNoWinners()
    {
        // A rank nobody reached still gets asked about, so this must not divide by zero.
        PrizeDistributionHelper.DistributePrizeMoney(100m, 0).Should().BeEmpty();
    }

    [Fact]
    public void DistributePrizeMoney_ShouldGiveTheWholePrizeToASingleWinner()
    {
        PrizeDistributionHelper.DistributePrizeMoney(100m, 1).Should().Equal(100m);
    }

    [Fact]
    public void DistributePrizeMoney_ShouldSplitEvenlyWhenItDividesCleanly()
    {
        PrizeDistributionHelper.DistributePrizeMoney(100m, 4).Should().Equal(25m, 25m, 25m, 25m);
    }

    [Fact]
    public void DistributePrizeMoney_ShouldGiveTheOddPennyToExactlyOneWinner()
    {
        // £100 between 3 is £33.33 each with a penny left over. Dropping it would leave the league
        // a penny short of what it collected, so it goes to someone - picked at random rather than
        // always the first, so nobody is systematically favoured.
        var shares = PrizeDistributionHelper.DistributePrizeMoney(100m, 3);

        shares.Sum().Should().Be(100m);
        shares.Count(s => s == 33.34m).Should().Be(1);
        shares.Count(s => s == 33.33m).Should().Be(2);
    }

    [Fact]
    public void DistributePrizeMoney_ShouldSpreadSeveralOddPenniesOnePerWinner()
    {
        // Two pennies over three winners must land on two different people, never both on one.
        var shares = PrizeDistributionHelper.DistributePrizeMoney(10.01m, 3);

        shares.Sum().Should().Be(10.01m);
        shares.Count(s => s == 3.34m).Should().Be(2);
        shares.Count(s => s == 3.33m).Should().Be(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    public void DistributePrizeMoney_ShouldAlwaysAddBackUpToTheOriginalAmount(int winnerCount)
    {
        var shares = PrizeDistributionHelper.DistributePrizeMoney(10m, winnerCount);

        shares.Should().HaveCount(winnerCount);
        shares.Sum().Should().Be(10m);
    }

    [Fact]
    public void DistributePrizeMoney_ShouldGiveEveryoneNothing_WhenThereIsNoPrize()
    {
        PrizeDistributionHelper.DistributePrizeMoney(0m, 3).Should().Equal(0m, 0m, 0m);
    }
}
