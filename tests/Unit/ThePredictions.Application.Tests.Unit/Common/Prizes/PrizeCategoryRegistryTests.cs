using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class PrizeCategoryRegistryTests
{
    [Fact]
    public void Definition_ShouldReturnDefinition_ForKnownCategory()
    {
        var definition = PrizeCategoryRegistry.Definition(PrizeType.Overall);

        definition.Kind.Should().Be(PrizeCategoryKind.EndOfSeason);
        definition.IsRanked.Should().BeTrue();
        definition.DefaultWeight.Should().Be(3);
    }

    [Theory]
    [InlineData(PrizeType.Overall, false, true)]
    [InlineData(PrizeType.Overall, true, true)]
    [InlineData(PrizeType.Monthly, false, true)]
    [InlineData(PrizeType.Monthly, true, false)]
    [InlineData(PrizeType.Section, true, true)]
    [InlineData(PrizeType.Section, false, false)]
    public void IsAvailable_ShouldGateByCompetitionType(PrizeType category, bool isTournament, bool expected)
    {
        PrizeCategoryRegistry.IsAvailable(category, isTournament).Should().Be(expected);
    }

    [Fact]
    public void AvailableCategories_ShouldHideMonthly_ForTournaments()
    {
        var categories = PrizeCategoryRegistry.AvailableCategories(isTournament: true).Select(d => d.Category).ToList();

        categories.Should().Contain(PrizeType.Section);
        categories.Should().NotContain(PrizeType.Monthly);
    }

    [Fact]
    public void AvailableCategories_ShouldHideSection_ForSeasons()
    {
        var categories = PrizeCategoryRegistry.AvailableCategories(isTournament: false).Select(d => d.Category).ToList();

        categories.Should().Contain(PrizeType.Monthly);
        categories.Should().NotContain(PrizeType.Section);
    }

    [Fact]
    public void RecommendedAllocation_ShouldSumToStake()
    {
        var allocation = PrizeCategoryRegistry.RecommendedAllocation(new[] { PrizeType.Overall, PrizeType.Round, PrizeType.MostExactScores }, 13);

        allocation.Values.Sum().Should().Be(13);
    }

    [Fact]
    public void RecommendedAllocation_ShouldFavourHigherWeights()
    {
        // Weights Overall 3 : Exact 1 -> 75/25 of £8 -> £6/£2.
        var allocation = PrizeCategoryRegistry.RecommendedAllocation(new[] { PrizeType.Overall, PrizeType.MostExactScores }, 8);

        allocation[PrizeType.Overall].Should().Be(6);
        allocation[PrizeType.MostExactScores].Should().Be(2);
    }

    [Fact]
    public void RecommendedAllocation_ShouldGiveAllToSingleCategory()
    {
        var allocation = PrizeCategoryRegistry.RecommendedAllocation(new[] { PrizeType.Overall }, 20);
        allocation[PrizeType.Overall].Should().Be(20);
    }

    [Fact]
    public void RecommendedAllocation_ShouldReturnEmpty_WhenNoCategories()
    {
        PrizeCategoryRegistry.RecommendedAllocation(Array.Empty<PrizeType>(), 13).Should().BeEmpty();
    }

    [Fact]
    public void RecommendedAllocation_ShouldReturnZeros_WhenStakeIsZero()
    {
        var allocation = PrizeCategoryRegistry.RecommendedAllocation(new[] { PrizeType.Overall, PrizeType.Round }, 0);
        allocation.Values.Should().OnlyContain(v => v == 0);
    }
}
