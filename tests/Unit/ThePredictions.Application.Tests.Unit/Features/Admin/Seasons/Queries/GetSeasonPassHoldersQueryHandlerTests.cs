using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Queries;

/// <summary>
/// One page of a season's pass holders.
///
/// The filtering, sorting and paging themselves stay in the read - choosing which rows to return is fetching, and a page
/// cannot be taken without sorting first - so what these tests are about is the handler's own job: keeping a page number in
/// range, snapping the page size, and handing the same filters to both reads so the count and the rows cannot disagree.
/// The wildcard escaping the handler used to do went the other way, into the adapter, because it is LIKE syntax.
/// </summary>
public class GetSeasonPassHoldersQueryHandlerTests
{
    private const int SeasonId = 7;

    private static readonly DateTime AcquiredAt = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonPassHoldersQuery _query = Substitute.For<ISeasonPassHoldersQuery>();
    private readonly GetSeasonPassHoldersQueryHandler _handler;

    public GetSeasonPassHoldersQueryHandlerTests()
    {
        _handler = new GetSeasonPassHoldersQueryHandler(_query);
    }

    [Fact]
    public async Task Handle_ShouldReportNotFound_WhenThereIsNoSuchSeason()
    {
        // Arrange
        _query.GetSummaryAsync(Arg.Any<SeasonPassHoldersCriteria>(), Arg.Any<CancellationToken>())
            .Returns((SeasonPassHoldersSummary?)null);

        // Act
        var act = () => HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyPage_WhenNothingMatchesTheFilters()
    {
        // Arrange
        GivenSummary(matchingCount: 0, totalCollected: 0m);

        // Act
        var page = await HandleAsync();

        // Assert
        page.SeasonName.Should().Be("World Cup 2026");
        page.TotalCollected.Should().Be(0m);
        page.Holders.Items.Should().BeEmpty();
        page.Holders.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotReadAnyRows_WhenNothingMatchesTheFilters()
    {
        // Arrange
        GivenSummary(matchingCount: 0, totalCollected: 0m);

        // Act
        await HandleAsync();

        // Assert - no point asking for page one of nothing.
        await _query.DidNotReceiveWithAnyArgs().GetPageAsync(default!, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReportTheTotalsFromTheSummaryRatherThanThePage()
    {
        // The totals are for everybody the filters match, not for the twenty-five on screen.
        GivenSummary(matchingCount: 40, totalCollected: 412.50m);
        GivenPage(Holder("u1"));

        // Act
        var page = await HandleAsync();

        // Assert
        page.TotalCollected.Should().Be(412.50m);
        page.Holders.TotalCount.Should().Be(40);
    }

    [Fact]
    public async Task Handle_ShouldReportEveryFieldOfEachHolder()
    {
        // Arrange
        GivenSummary(matchingCount: 1, totalCollected: 12m);
        GivenPage(new SeasonPassHolderRow("u1", "Ada Lovelace", "ada@example.com",
            SeasonPassTier.Premium, SeasonPassSource.Purchased, 10m, 2m, AcquiredAt));

        // Act
        var holder = (await HandleAsync()).Holders.Items.Single();

        // Assert
        holder.UserId.Should().Be("u1");
        holder.FullName.Should().Be("Ada Lovelace");
        holder.Email.Should().Be("ada@example.com");
        holder.Tier.Should().Be(SeasonPassTier.Premium);
        holder.Source.Should().Be(SeasonPassSource.Purchased);
        holder.AmountPaid.Should().Be(10m);
        holder.SmsFeePaid.Should().Be(2m);
        holder.AcquiredAtUtc.Should().Be(AcquiredAt);
    }

    [Theory]
    [InlineData(0, PageSizes.Default)]
    [InlineData(7, PageSizes.Default)]
    [InlineData(50, 50)]
    [InlineData(1000, PageSizes.Default)]
    public async Task Handle_ShouldSnapThePageSizeToAnAllowedValue(int requested, int expected)
    {
        // Arrange
        GivenSummary(matchingCount: 200, totalCollected: 0m);
        GivenPage();

        // Act
        var page = await HandleAsync(pageSize: requested);

        // Assert
        page.Holders.PageSize.Should().Be(expected);
        await _query.Received(1).GetPageAsync(
            Arg.Any<SeasonPassHoldersCriteria>(),
            Arg.Is<SeasonPassHoldersPaging>(paging => paging.Take == expected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldClampThePageToTheLastOne_WhenAskedForOneBeyondTheEnd()
    {
        // Tightening a filter should land on the last page of the smaller result set, not on an empty one.
        GivenSummary(matchingCount: 30, totalCollected: 0m);
        GivenPage();

        // Act
        var page = await HandleAsync(page: 9, pageSize: PageSizes.Default);

        // Assert - thirty holders at twenty-five a page is two pages.
        page.Holders.Page.Should().Be(2);
        await _query.Received(1).GetPageAsync(
            Arg.Any<SeasonPassHoldersCriteria>(),
            Arg.Is<SeasonPassHoldersPaging>(paging => paging.Skip == PageSizes.Default),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Handle_ShouldClampThePageToOne_WhenAskedForOneBeforeTheStart(int requestedPage)
    {
        // Arrange
        GivenSummary(matchingCount: 30, totalCollected: 0m);
        GivenPage();

        // Act
        var page = await HandleAsync(page: requestedPage);

        // Assert
        page.Holders.Page.Should().Be(1);
        await _query.Received(1).GetPageAsync(
            Arg.Any<SeasonPassHoldersCriteria>(),
            Arg.Is<SeasonPassHoldersPaging>(paging => paging.Skip == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipTheEarlierPages_WhenAskedForOneInTheMiddle()
    {
        // Arrange
        GivenSummary(matchingCount: 200, totalCollected: 0m);
        GivenPage();

        // Act
        await HandleAsync(page: 3, pageSize: 50);

        // Assert
        await _query.Received(1).GetPageAsync(
            Arg.Any<SeasonPassHoldersCriteria>(),
            Arg.Is<SeasonPassHoldersPaging>(paging => paging.Skip == 100 && paging.Take == 50),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SeasonPassHolderSortField.Name)]
    [InlineData(SeasonPassHolderSortField.AcquiredAt)]
    [InlineData(SeasonPassHolderSortField.TotalPaid)]
    public async Task Handle_ShouldPassTheSortFieldThrough(SeasonPassHolderSortField sortField)
    {
        // Arrange
        GivenSummary(matchingCount: 1, totalCollected: 0m);
        GivenPage();

        // Act
        await HandleAsync(sortField: sortField);

        // Assert
        await _query.Received(1).GetPageAsync(
            Arg.Any<SeasonPassHoldersCriteria>(),
            Arg.Is<SeasonPassHoldersPaging>(paging => paging.SortField == sortField),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task Handle_ShouldPassTheSortDirectionThrough(SortDirection direction)
    {
        // Arrange
        GivenSummary(matchingCount: 1, totalCollected: 0m);
        GivenPage();

        // Act
        await HandleAsync(sortDirection: direction);

        // Assert
        await _query.Received(1).GetPageAsync(
            Arg.Any<SeasonPassHoldersCriteria>(),
            Arg.Is<SeasonPassHoldersPaging>(paging => paging.SortDirection == direction),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassTheFiltersThroughUntouched()
    {
        // Including the name exactly as typed. What needs escaping depends on how the adapter searches, so the adapter
        // does it - the handler passing a half-escaped string would be guessing on its behalf.
        GivenSummary(matchingCount: 1, totalCollected: 0m);
        GivenPage();

        // Act
        await HandleAsync(
            nameFilter: "  100% Ada  ",
            acquiredFromUtc: AcquiredAt,
            acquiredBeforeUtc: AcquiredAt.AddDays(1),
            minimumPaid: 5m,
            maximumPaid: 50m);

        // Assert
        await _query.Received(1).GetSummaryAsync(
            Arg.Is<SeasonPassHoldersCriteria>(criteria =>
                criteria.SeasonId == SeasonId
                && criteria.NameFilter == "  100% Ada  "
                && criteria.AcquiredFromUtc == AcquiredAt
                && criteria.AcquiredBeforeUtc == AcquiredAt.AddDays(1)
                && criteria.MinimumPaid == 5m
                && criteria.MaximumPaid == 50m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldApplyTheSameFiltersToTheTotalsAndToTheRows()
    {
        // If the two disagreed, the page would show a count that its own rows contradict.
        GivenSummary(matchingCount: 1, totalCollected: 0m);
        GivenPage();

        // Act
        await HandleAsync(nameFilter: "Ada", minimumPaid: 5m);

        // Assert
        var expected = new SeasonPassHoldersCriteria(SeasonId, "Ada", null, null, 5m, null);

        await _query.Received(1).GetSummaryAsync(expected, Arg.Any<CancellationToken>());
        await _query.Received(1).GetPageAsync(expected, Arg.Any<SeasonPassHoldersPaging>(), Arg.Any<CancellationToken>());
    }

    private void GivenSummary(int matchingCount, decimal totalCollected) =>
        _query.GetSummaryAsync(Arg.Any<SeasonPassHoldersCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new SeasonPassHoldersSummary("World Cup 2026", matchingCount, totalCollected));

    private void GivenPage(params SeasonPassHolderRow[] holders) =>
        _query.GetPageAsync(Arg.Any<SeasonPassHoldersCriteria>(), Arg.Any<SeasonPassHoldersPaging>(), Arg.Any<CancellationToken>())
            .Returns(holders);

    private static SeasonPassHolderRow Holder(string userId) =>
        new(userId, "Ada Lovelace", "ada@example.com", SeasonPassTier.Standard, SeasonPassSource.Purchased, 10m, 0m, AcquiredAt);

    private Task<SeasonPassHoldersPageDto> HandleAsync(
        int page = 1,
        int pageSize = PageSizes.Default,
        string? nameFilter = null,
        DateTime? acquiredFromUtc = null,
        DateTime? acquiredBeforeUtc = null,
        decimal? minimumPaid = null,
        decimal? maximumPaid = null,
        SeasonPassHolderSortField sortField = SeasonPassHolderSortField.Name,
        SortDirection sortDirection = SortDirection.Ascending) =>
        _handler.Handle(
            new GetSeasonPassHoldersQuery(
                SeasonId, page, pageSize, sortField, sortDirection, nameFilter,
                acquiredFromUtc, acquiredBeforeUtc, minimumPaid, maximumPaid),
            CancellationToken.None);
}
