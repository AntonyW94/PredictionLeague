using FluentAssertions;
using ThePredictions.Contracts.Common;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Common;

public class PagedResultTests
{
    private static PagedResult<string> Page(int page, int pageSize, int totalCount, params string[] items) =>
        new(items, page, pageSize, totalCount);

    [Fact]
    public void Empty_ShouldReadAsPageOneOfOneWithNothingOnIt()
    {
        var result = PagedResult<string>.Empty(25);

        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(25);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(1);
        result.FirstItemNumber.Should().Be(0);
        result.LastItemNumber.Should().Be(0);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TotalPages_ShouldBeOne_WhenThereAreNoResults(int totalCount)
    {
        Page(1, 10, totalCount).TotalPages.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(20, 10, 2)]
    [InlineData(21, 10, 3)]
    public void TotalPages_ShouldRoundUpToCoverEveryItem(int totalCount, int pageSize, int expected)
    {
        Page(1, pageSize, totalCount).TotalPages.Should().Be(expected);
    }

    [Fact]
    public void HasPreviousPage_ShouldBeFalse_OnTheFirstPage()
    {
        Page(1, 10, 50).HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_ShouldBeTrue_BeyondTheFirstPage()
    {
        Page(2, 10, 50).HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_ShouldBeTrue_WhenThereAreLaterPages()
    {
        Page(1, 10, 50).HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_ShouldBeFalse_OnTheLastPage()
    {
        Page(5, 10, 50).HasNextPage.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 10, 1)]
    [InlineData(2, 10, 11)]
    [InlineData(3, 25, 51)]
    public void FirstItemNumber_ShouldBeOneBased(int page, int pageSize, int expected)
    {
        Page(page, pageSize, 500).FirstItemNumber.Should().Be(expected);
    }

    [Fact]
    public void FirstItemNumber_ShouldBeZero_WhenThereAreNoResults()
    {
        Page(1, 10, 0).FirstItemNumber.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 10, 50, 10)]
    [InlineData(2, 10, 50, 20)]
    [InlineData(5, 10, 45, 45)]
    public void LastItemNumber_ShouldNotRunPastTheTotal(int page, int pageSize, int totalCount, int expected)
    {
        Page(page, pageSize, totalCount).LastItemNumber.Should().Be(expected);
    }

    [Fact]
    public void LastItemNumber_ShouldBeZero_WhenThereAreNoResults()
    {
        Page(1, 10, 0).LastItemNumber.Should().Be(0);
    }

    [Fact]
    public void APartialFinalPage_ShouldReportTheRealRange()
    {
        var result = Page(3, 10, 23, "a", "b", "c");

        result.TotalPages.Should().Be(3);
        result.FirstItemNumber.Should().Be(21);
        result.LastItemNumber.Should().Be(23);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }
}
