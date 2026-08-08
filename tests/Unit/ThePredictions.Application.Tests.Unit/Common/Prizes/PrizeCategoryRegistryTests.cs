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
    [InlineData(PrizeType.Stages, true, true)]
    [InlineData(PrizeType.Stages, false, false)]
    public void IsAvailable_ShouldGateByCompetitionType(PrizeType category, bool isTournament, bool expected)
    {
        PrizeCategoryRegistry.IsAvailable(category, isTournament).Should().Be(expected);
    }

    [Fact]
    public void AvailableCategories_ShouldHideMonthly_ForTournaments()
    {
        var categories = PrizeCategoryRegistry.AvailableCategories(isTournament: true).Select(d => d.Category).ToList();

        categories.Should().Contain(PrizeType.Stages);
        categories.Should().NotContain(PrizeType.Monthly);
    }

    [Fact]
    public void AvailableCategories_ShouldHideSection_ForSeasons()
    {
        var categories = PrizeCategoryRegistry.AvailableCategories(isTournament: false).Select(d => d.Category).ToList();

        categories.Should().Contain(PrizeType.Monthly);
        categories.Should().NotContain(PrizeType.Stages);
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

    [Fact]
    public void Definition_ShouldThrow_ForACategoryThatIsNotRegistered()
    {
        // Defensive: a new PrizeType added without a definition must fail loudly rather than
        // silently producing a prize nobody configured.
        var act = () => PrizeCategoryRegistry.Definition((PrizeType)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(PrizeType.Overall, false, true)]
    [InlineData(PrizeType.Overall, true, true)]
    [InlineData(PrizeType.Monthly, false, true)]
    [InlineData(PrizeType.Monthly, true, false)]
    [InlineData(PrizeType.Stages, false, false)]
    [InlineData(PrizeType.Stages, true, true)]
    public void IsAvailable_ShouldOfferOnlyTheCategoriesThatMakeSenseForTheCompetition(
        PrizeType category, bool isTournament, bool expected)
    {
        // A knockout cup has no calendar months to win, and a league season has no stages, so each
        // is offered only where it means something.
        PrizeCategoryRegistry.IsAvailable(category, isTournament).Should().Be(expected);
    }
}
