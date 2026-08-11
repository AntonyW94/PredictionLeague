using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services.Prizes;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Prizes;

/// <summary>
/// Which prizes are settled at the end of the season rather than as it runs - the bucket the winnings page stated three
/// times as a chain of negations.
/// </summary>
public class PrizeCategoryRegistryEndOfSeasonTests
{
    [Theory]
    [InlineData(PrizeType.Overall)]
    [InlineData(PrizeType.MostExactScores)]
    public void IsEndOfSeason_ShouldBeTrue_ForAPrizeDecidedWhenTheSeasonEnds(PrizeType category)
    {
        PrizeCategoryRegistry.IsEndOfSeason(category).Should().BeTrue();
    }

    [Theory]
    [InlineData(PrizeType.Round)]
    [InlineData(PrizeType.Monthly)]
    [InlineData(PrizeType.Stages)]
    public void IsEndOfSeason_ShouldBeFalse_ForAPrizeDecidedAsTheSeasonRuns(PrizeType category)
    {
        // These three have their own lists on the winnings page, which is what makes the fourth "everything else".
        PrizeCategoryRegistry.IsEndOfSeason(category).Should().BeFalse();
    }
}
