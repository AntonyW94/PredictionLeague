using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeaguePrizeSchemeEntryTests
{
    [Fact]
    public void Create_ShouldCreateEntry_WhenValid()
    {
        var entry = LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8);

        entry.Category.Should().Be(PrizeType.Overall);
        entry.PerEntryPounds.Should().Be(8);
        entry.RankTableJson.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldStoreRankTableJson_WhenProvided()
    {
        var entry = LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8, "[{\"min\":2}]");
        entry.RankTableJson.Should().Be("[{\"min\":2}]");
    }

    [Fact]
    public void Create_ShouldAllowZeroPerEntryPounds()
    {
        var entry = LeaguePrizeSchemeEntry.Create(PrizeType.MostExactScores, 0);
        entry.PerEntryPounds.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldThrow_WhenPerEntryPoundsNegative()
    {
        var act = () => LeaguePrizeSchemeEntry.Create(PrizeType.Overall, -1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignToScheme_ShouldSetForeignKey()
    {
        var entry = LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8);
        entry.AssignToScheme(42);
        entry.LeaguePrizeSchemeId.Should().Be(42);
    }

    [Fact]
    public void AssignToScheme_ShouldThrow_WhenIdIsZero()
    {
        var entry = LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8);
        var act = () => entry.AssignToScheme(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HydrationConstructor_ShouldSetAllFields()
    {
        var entry = new LeaguePrizeSchemeEntry(5, 42, PrizeType.Round, 3, "json");

        entry.Id.Should().Be(5);
        entry.LeaguePrizeSchemeId.Should().Be(42);
        entry.Category.Should().Be(PrizeType.Round);
        entry.PerEntryPounds.Should().Be(3);
        entry.RankTableJson.Should().Be("json");
    }
}
