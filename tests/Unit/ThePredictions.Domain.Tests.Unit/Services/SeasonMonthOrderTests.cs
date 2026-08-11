using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// A season's months in the order the season runs them, not in calendar order - the rule that stops an
/// August-to-May season listing January first.
/// </summary>
public class SeasonMonthOrderTests
{
    private static IEnumerable<int> Ordered(int seasonStartMonth, params int[] months) =>
        SeasonMonthOrder.Apply(months, month => month, seasonStartMonth);

    [Fact]
    public void Apply_ShouldRunFromTheSeasonsFirstMonthAndWrapIntoTheNewYear()
    {
        // A season starting in August: August to December, then January onwards.
        Ordered(8, 1, 2, 8, 9, 12).Should().Equal(8, 9, 12, 1, 2);
    }

    [Fact]
    public void Apply_ShouldLeaveACalendarYearSeasonInCalendarOrder()
    {
        // Starting in January there is nothing to wrap.
        Ordered(1, 3, 1, 2).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Apply_ShouldPutTheSeasonsFirstMonthFirst_EvenWhenItIsDecember()
    {
        Ordered(12, 1, 12, 6).Should().Equal(12, 1, 6);
    }

    [Fact]
    public void Apply_ShouldHandleASeasonOfOneMonth()
    {
        Ordered(6, 6).Should().Equal(6);
    }

    [Fact]
    public void Apply_ShouldReturnNothing_ForNoMonths()
    {
        Ordered(8).Should().BeEmpty();
    }
}
