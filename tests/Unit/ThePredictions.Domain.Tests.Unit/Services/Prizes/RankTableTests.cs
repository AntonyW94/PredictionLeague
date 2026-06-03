using FluentAssertions;
using ThePredictions.Domain.Services.Prizes;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Prizes;

public class RankTableTests
{
    [Fact]
    public void Constructor_ShouldOrderBandsByMinEntrants()
    {
        var table = new RankTable(new[]
        {
            new RankBand(11, 20, new[] { 50, 30, 20 }),
            new RankBand(2, 10, new[] { 100 })
        });

        table.Bands.First().MinEntrants.Should().Be(2);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBandsNull()
    {
        var act = () => new RankTable(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNoBands()
    {
        var act = () => new RankTable(Array.Empty<RankBand>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNonFinalBandIsOpenEnded()
    {
        var act = () => new RankTable(new[]
        {
            new RankBand(2, null, new[] { 100 }),
            new RankBand(11, 20, new[] { 50, 30, 20 })
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBandsOverlap()
    {
        var act = () => new RankTable(new[]
        {
            new RankBand(2, 10, new[] { 100 }),
            new RankBand(10, 20, new[] { 50, 30, 20 })
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PercentagesFor_ShouldReturnMatchingBand()
    {
        var table = new RankTable(new[]
        {
            new RankBand(2, 5, new[] { 100 }),
            new RankBand(6, 10, new[] { 70, 30 })
        });

        table.PercentagesFor(8).Should().Equal(70, 30);
    }

    [Fact]
    public void PercentagesFor_ShouldReturnSinglePlace_WhenNoBandMatches()
    {
        var table = new RankTable(new[]
        {
            new RankBand(6, 10, new[] { 70, 30 })
        });

        table.PercentagesFor(1).Should().Equal(100);
    }
}
