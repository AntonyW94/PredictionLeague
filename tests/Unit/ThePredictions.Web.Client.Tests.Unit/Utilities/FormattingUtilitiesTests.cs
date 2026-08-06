using FluentAssertions;
using ThePredictions.Web.Client.Utilities;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Utilities;

public class FormattingUtilitiesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GetOrdinal_ShouldReturnNothing_ForANonPositivePosition(int number)
    {
        FormattingUtilities.GetOrdinal(number).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, "st")]
    [InlineData(2, "nd")]
    [InlineData(3, "rd")]
    [InlineData(4, "th")]
    [InlineData(9, "th")]
    [InlineData(10, "th")]
    public void GetOrdinal_ShouldUseTheUsualSuffixes_ForSingleDigits(int number, string expected)
    {
        FormattingUtilities.GetOrdinal(number).Should().Be(expected);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(111)]
    [InlineData(112)]
    [InlineData(113)]
    [InlineData(1011)]
    public void GetOrdinal_ShouldUseTh_ForTheTeensException(int number)
    {
        // 11th, not 11st - and the same for every century that ends in 11 to 13.
        FormattingUtilities.GetOrdinal(number).Should().Be("th");
    }

    [Theory]
    [InlineData(21, "st")]
    [InlineData(22, "nd")]
    [InlineData(23, "rd")]
    [InlineData(101, "st")]
    [InlineData(102, "nd")]
    [InlineData(103, "rd")]
    [InlineData(121, "st")]
    public void GetOrdinal_ShouldResumeTheUsualSuffixes_PastTheTeens(int number, string expected)
    {
        FormattingUtilities.GetOrdinal(number).Should().Be(expected);
    }

    [Fact]
    public void GetOrdinal_ShouldHandleAVeryLargePosition()
    {
        FormattingUtilities.GetOrdinal(int.MaxValue).Should().Be("th");
    }
}
