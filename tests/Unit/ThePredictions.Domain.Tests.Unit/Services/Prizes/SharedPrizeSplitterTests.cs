using FluentAssertions;
using ThePredictions.Domain.Services.Prizes;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Prizes;

/// <summary>
/// Splits a prize between joint winners. Real money, so the parts must add back up to exactly the
/// original amount - any odd penny goes to someone rather than disappearing.
/// </summary>
public class SharedPrizeSplitterTests
{
    [Fact]
    public void Split_ShouldReturnNothing_WhenThereAreNoWinners()
    {
        // A rank nobody reached still gets asked about, so this must not divide by zero.
        SharedPrizeSplitter.Split(100m, 0).Should().BeEmpty();
    }

    [Fact]
    public void Split_ShouldGiveTheWholePrizeToASingleWinner()
    {
        SharedPrizeSplitter.Split(100m, 1).Should().Equal(100m);
    }

    [Fact]
    public void Split_ShouldSplitEvenlyWhenItDividesCleanly()
    {
        SharedPrizeSplitter.Split(100m, 4).Should().Equal(25m, 25m, 25m, 25m);
    }

    [Fact]
    public void Split_ShouldGiveTheOddPennyToExactlyOneWinner()
    {
        // £100 between 3 is £33.33 each with a penny left over. Dropping it would leave the league
        // a penny short of what it collected, so it goes to someone - picked at random rather than
        // always the first, so nobody is systematically favoured.
        var shares = SharedPrizeSplitter.Split(100m, 3);

        shares.Sum().Should().Be(100m);
        shares.Count(s => s == 33.34m).Should().Be(1);
        shares.Count(s => s == 33.33m).Should().Be(2);
    }

    [Fact]
    public void Split_ShouldSpreadSeveralOddPenniesOnePerWinner()
    {
        // Two pennies over three winners must land on two different people, never both on one.
        var shares = SharedPrizeSplitter.Split(10.01m, 3);

        shares.Sum().Should().Be(10.01m);
        shares.Count(s => s == 3.34m).Should().Be(2);
        shares.Count(s => s == 3.33m).Should().Be(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    public void Split_ShouldAlwaysAddBackUpToTheOriginalAmount(int winnerCount)
    {
        var shares = SharedPrizeSplitter.Split(10m, winnerCount);

        shares.Should().HaveCount(winnerCount);
        shares.Sum().Should().Be(10m);
    }

    [Fact]
    public void Split_ShouldGiveEveryoneNothing_WhenThereIsNoPrize()
    {
        SharedPrizeSplitter.Split(0m, 3).Should().Equal(0m, 0m, 0m);
    }
}
