using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// A month number as its English name. Two queries asked <c>DATENAME(MONTH, ...)</c> for this, so the answer came
/// out in whatever language the database login happened to be configured with.
/// </summary>
public class MonthNameTests
{
    [Theory]
    [InlineData(1, "January")]
    [InlineData(3, "March")]
    [InlineData(12, "December")]
    public void Of_ShouldNameTheMonth(int month, string expected)
    {
        MonthName.Of(month).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(null)]
    public void Of_ShouldReturnNothing_ForAMonthThatCannotExist(int? month)
    {
        // One bad stored value should not take a whole page down, which is what DATEFROMPARTS did.
        MonthName.Of(month).Should().BeNull();
    }
}
