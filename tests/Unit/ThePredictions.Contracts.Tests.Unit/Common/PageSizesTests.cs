using FluentAssertions;
using ThePredictions.Contracts.Common;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Common;

public class PageSizesTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void Clamp_ShouldKeepAnAllowedSize(int pageSize)
    {
        PageSizes.Clamp(pageSize).Should().Be(pageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(26)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void Clamp_ShouldFallBackToTheDefault_ForAnythingElse(int pageSize)
    {
        PageSizes.Clamp(pageSize).Should().Be(PageSizes.Default);
    }

    [Fact]
    public void TheDefaultShouldItselfBeAnAllowedSize()
    {
        // Otherwise clamping a rejected value would produce another rejected value.
        PageSizes.Allowed.Should().Contain(PageSizes.Default);
    }

    [Fact]
    public void TheAllowedSizesShouldBeAscendingAndDistinct()
    {
        PageSizes.Allowed.Should().OnlyHaveUniqueItems();
        PageSizes.Allowed.Should().BeInAscendingOrder();
        PageSizes.Allowed.Should().OnlyContain(s => s > 0);
    }
}
