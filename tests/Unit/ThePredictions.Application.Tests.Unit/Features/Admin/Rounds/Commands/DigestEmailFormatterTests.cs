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
    [InlineData(1, "up 1")]
    [InlineData(5, "up 5")]
    [InlineData(-1, "down 1")]
    [InlineData(-4, "down 4")]
    [InlineData(0, "no change")]
    public void PositionMovement_ShouldDescribeDelta(int delta, string expected)
    {
        DigestEmailFormatter.PositionMovement(delta).Should().Be(expected);
    }

    [Fact]
    public void PositionMovement_ShouldReturnEmpty_WhenDeltaIsNull()
    {
        DigestEmailFormatter.PositionMovement(null).Should().BeEmpty();
    }
}
