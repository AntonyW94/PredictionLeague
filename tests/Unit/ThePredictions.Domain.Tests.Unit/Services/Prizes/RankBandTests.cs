using FluentAssertions;
using ThePredictions.Domain.Services.Prizes;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Prizes;

public class RankBandTests
{
    [Fact]
    public void Constructor_ShouldCreateBand_WhenValid()
    {
        var band = new RankBand(6, 10, new[] { 70, 30 });

        band.MinEntrants.Should().Be(6);
        band.MaxEntrants.Should().Be(10);
        band.Percentages.Should().Equal(70, 30);
    }

    [Fact]
    public void Constructor_ShouldAllowOpenEndedBand_WhenMaxIsNull()
    {
        var band = new RankBand(76, null, new[] { 100 });
        band.MaxEntrants.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMinEntrantsIsZero()
    {
        var act = () => new RankBand(0, 5, new[] { 100 });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPercentagesNull()
    {
        var act = () => new RankBand(2, 5, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMaxLessThanMin()
    {
        var act = () => new RankBand(10, 5, new[] { 100 });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNoPlaces()
    {
        var act = () => new RankBand(2, 5, Array.Empty<int>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAnyPercentageIsZeroOrNegative()
    {
        var act = () => new RankBand(2, 5, new[] { 100, 0 });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPercentagesDoNotSumTo100()
    {
        var act = () => new RankBand(2, 5, new[] { 60, 30 });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPercentagesNotDescending()
    {
        var act = () => new RankBand(2, 5, new[] { 30, 70 });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldAllowEqualAdjacentPercentages()
    {
        var band = new RankBand(2, 5, new[] { 50, 50 });
        band.Percentages.Should().Equal(50, 50);
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(10, true)]
    [InlineData(5, false)]
    [InlineData(11, false)]
    public void Contains_ShouldRespectBounds(int entrants, bool expected)
    {
        var band = new RankBand(6, 10, new[] { 70, 30 });
        band.Contains(entrants).Should().Be(expected);
    }

    [Fact]
    public void Contains_ShouldBeTrueForAnyCountAtOrAboveMin_WhenOpenEnded()
    {
        var band = new RankBand(76, null, new[] { 100 });
        band.Contains(1000).Should().BeTrue();
        band.Contains(75).Should().BeFalse();
    }
}
