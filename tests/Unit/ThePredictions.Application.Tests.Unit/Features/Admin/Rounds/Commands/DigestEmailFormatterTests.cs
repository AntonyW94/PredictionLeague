using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

public class DigestEmailFormatterTests
{
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(101, "101st")]
    [InlineData(111, "111th")]
    public void Ordinal_ShouldReturnCorrectSuffix(int position, string expected)
    {
        DigestEmailFormatter.Ordinal(position).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void Ordinal_ShouldReturnEmpty_WhenPositionMissingOrInvalid(int? position)
    {
        DigestEmailFormatter.Ordinal(position).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, "▲")]
    [InlineData(5, "▲")]
    [InlineData(-1, "▼")]
    [InlineData(-4, "▼")]
    [InlineData(0, "-")]
    public void MovementArrow_ShouldReflectDirection(int delta, string expected)
    {
        DigestEmailFormatter.MovementArrow(delta).Should().Be(expected);
    }

    [Fact]
    public void MovementArrow_ShouldReturnEmpty_WhenDeltaIsNull()
    {
        DigestEmailFormatter.MovementArrow(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData(3, "#00B960")]
    [InlineData(-2, "#E90052")]
    [InlineData(0, "#98a2b3")]
    public void MovementColour_ShouldReflectDirection(int delta, string expected)
    {
        DigestEmailFormatter.MovementColour(delta).Should().Be(expected);
    }

    [Fact]
    public void MovementColour_ShouldReturnEmpty_WhenDeltaIsNull()
    {
        DigestEmailFormatter.MovementColour(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(5, "5")]
    [InlineData(-4, "4")]
    public void MovementCount_ShouldBeAbsoluteMagnitude(int delta, string expected)
    {
        DigestEmailFormatter.MovementCount(delta).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public void MovementCount_ShouldBeEmpty_ForNoChangeOrNull(int? delta)
    {
        DigestEmailFormatter.MovementCount(delta).Should().BeEmpty();
    }
}
