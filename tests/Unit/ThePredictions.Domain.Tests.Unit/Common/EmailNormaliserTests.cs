using FluentAssertions;
using ThePredictions.Domain.Common;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common;

public class EmailNormaliserTests
{
    [Theory]
    [InlineData("you@x.com", "you@x.com")]
    [InlineData("  YOU@X.COM  ", "you@x.com")]
    [InlineData("you+tag@x.com", "you@x.com")]
    [InlineData("you+a+b@x.com", "you@x.com")]
    [InlineData("First.Last+promo@Gmail.com", "first.last@gmail.com")]
    public void ToCanonical_ShouldLowercaseTrimAndStripPlusAlias(string input, string expected)
    {
        EmailNormaliser.ToCanonical(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ToCanonical_ShouldReturnEmpty_WhenNullOrBlank(string? input, string expected)
    {
        EmailNormaliser.ToCanonical(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("noatsign", "noatsign")]      // no '@' - returned as-is (lowercased)
    [InlineData("@x.com", "@x.com")]          // leading '@' (empty local) - returned as-is
    public void ToCanonical_ShouldReturnInput_WhenNoUsableLocalPart(string input, string expected)
    {
        EmailNormaliser.ToCanonical(input).Should().Be(expected);
    }
}
