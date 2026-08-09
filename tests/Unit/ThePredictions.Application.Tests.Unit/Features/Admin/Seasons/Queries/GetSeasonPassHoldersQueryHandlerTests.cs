using ThePredictions.Domain.Common.Exceptions;
using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Features.Admin.Seasons.Queries.GetSeasonPassHoldersQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Queries;

public class GetSeasonPassHoldersQueryHandlerTests
{
    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly GetSeasonPassHoldersQueryHandler _handler;

    public GetSeasonPassHoldersQueryHandlerTests()
    {
        _handler = new GetSeasonPassHoldersQueryHandler(_dbConnection);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenSeasonDoesNotExist()
    {
        var act = () => _handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNothingMatches()
    {
        GivenSummary(matchingCount: 0, totalCollected: 0);

        var result = await _handler.Handle(Query(page: 3), CancellationToken.None);

        result.Should().NotBeNull();
        result.SeasonName.Should().Be("World Cup 2026");
        result.TotalCollected.Should().Be(0);
        result.Holders.Items.Should().BeEmpty();
        result.Holders.Page.Should().Be(1);
        result.Holders.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotReadTheRows_WhenNothingMatches()
    {
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(), CancellationToken.None);

        PageCallParameters().Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnTheMatchingCountAndTotal_WhenHoldersMatch()
    {
        GivenSummary(matchingCount: 33, totalCollected: 412.50m);

        var result = await _handler.Handle(Query(), CancellationToken.None);

        result.TotalCollected.Should().Be(412.50m);
        result.Holders.TotalCount.Should().Be(33);
    }

    [Fact]
    public async Task Handle_ShouldMapEveryFieldOntoTheHolder_WhenRowsAreReturned()
    {
        var acquired = new DateTime(2026, 7, 21, 8, 30, 0, DateTimeKind.Utc);
        GivenSummary(matchingCount: 1, totalCollected: 15m, Row(
            userId: "user-9",
            fullName: "Jane Doe",
            email: "jane@example.com",
            tier: SeasonPassTier.Standard,
            source: SeasonPassSource.Trial,
            amountPaid: 11m,
            smsFeePaid: 4m,
            createdAtUtc: acquired));

        var result = await _handler.Handle(Query(), CancellationToken.None);

        result.Holders.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new SeasonPassHolderDto("user-9", "Jane Doe", "jane@example.com", SeasonPassTier.Standard, SeasonPassSource.Trial, 11m, 4m, acquired));
    }

    [Theory]
    [InlineData(0, PageSizes.Default)]
    [InlineData(7, PageSizes.Default)]
    [InlineData(1000, PageSizes.Default)]
    [InlineData(5, 5)]
    [InlineData(100, 100)]
    public async Task Handle_ShouldSnapPageSizeToAnAllowedValue_WhenGivenAnySize(int requested, int expected)
    {
        GivenSummary(matchingCount: 500, totalCollected: 0);

        var result = await _handler.Handle(Query(pageSize: requested), CancellationToken.None);

        result.Holders.PageSize.Should().Be(expected);
        PageParameter("Take").Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldClampPageToTheLastPage_WhenRequestedPageIsBeyondTheEnd()
    {
        GivenSummary(matchingCount: 12, totalCollected: 0);

        var result = await _handler.Handle(Query(page: 99, pageSize: 5), CancellationToken.None);

        result.Holders.Page.Should().Be(3);
        PageParameter("Skip").Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task Handle_ShouldClampPageToOne_WhenRequestedPageIsNotPositive(int requestedPage)
    {
        GivenSummary(matchingCount: 12, totalCollected: 0);

        var result = await _handler.Handle(Query(page: requestedPage, pageSize: 5), CancellationToken.None);

        result.Holders.Page.Should().Be(1);
        PageParameter("Skip").Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSkipTheEarlierPages_WhenAskedForAMiddlePage()
    {
        GivenSummary(matchingCount: 60, totalCollected: 0);

        await _handler.Handle(Query(page: 3, pageSize: 25), CancellationToken.None);

        PageParameter("Skip").Should().Be(50);
    }

    [Theory]
    [InlineData(SeasonPassHolderSortField.Name)]
    [InlineData(SeasonPassHolderSortField.AcquiredAt)]
    [InlineData(SeasonPassHolderSortField.TotalPaid)]
    public async Task Handle_ShouldPassTheSortColumnByName_WhenSorting(SeasonPassHolderSortField sortField)
    {
        GivenSummary(matchingCount: 5, totalCollected: 0);

        await _handler.Handle(Query(sortField: sortField), CancellationToken.None);

        PageParameter("SortField").Should().Be(sortField.ToString());
    }

    [Theory]
    [InlineData(SortDirection.Ascending, false)]
    [InlineData(SortDirection.Descending, true)]
    public async Task Handle_ShouldTranslateTheSortDirection_WhenSorting(SortDirection direction, bool expectedDescending)
    {
        GivenSummary(matchingCount: 5, totalCollected: 0);

        await _handler.Handle(Query(sortDirection: direction), CancellationToken.None);

        PageParameter("SortDescending").Should().Be(expectedDescending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldNotFilterByName_WhenTheNameFilterIsBlank(string? nameFilter)
    {
        // The season has to exist for the handler to get as far as reading rows.
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(nameFilter: nameFilter), CancellationToken.None);

        SummaryParameter("NameFilter").Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldTrimTheNameFilter_WhenItHasSurroundingSpace()
    {
        // The season has to exist for the handler to get as far as reading rows.
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(nameFilter: "  Smith  "), CancellationToken.None);

        SummaryParameter("NameFilter").Should().Be("Smith");
    }

    [Theory]
    [InlineData("50%", "50[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[wat]", "[[]wat]")]
    [InlineData("100%_[x]", "100[%][_][[]x]")]
    public async Task Handle_ShouldEscapeLikeWildcards_WhenTheNameFilterContainsThem(string nameFilter, string expected)
    {
        // The season has to exist for the handler to get as far as reading rows.
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(nameFilter: nameFilter), CancellationToken.None);

        SummaryParameter("NameFilter").Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldUseTheDateBoundsExactlyAsGiven_WhenFilteringOnDate()
    {
        // Deliberately not midnight: the caller has already worked out where its day starts, so the
        // handler must not round these to a UTC day and undo that.
        var from = new DateTime(2026, 8, 3, 23, 0, 0, DateTimeKind.Utc);
        var before = new DateTime(2026, 8, 4, 23, 0, 0, DateTimeKind.Utc);

        // The season has to exist for the handler to get as far as reading rows.
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(acquiredFromUtc: from, acquiredBeforeUtc: before), CancellationToken.None);

        SummaryParameter("AcquiredFromUtc").Should().Be(from);
        SummaryParameter("AcquiredBeforeUtc").Should().Be(before);
    }

    [Fact]
    public async Task Handle_ShouldNotFilterByDate_WhenNoDatesAreGiven()
    {
        // The season has to exist for the handler to get as far as reading rows.
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(), CancellationToken.None);

        SummaryParameter("AcquiredFromUtc").Should().BeNull();
        SummaryParameter("AcquiredBeforeUtc").Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldPassThePriceRangeThrough_WhenFilteringOnPrice()
    {
        // The season has to exist for the handler to get as far as reading rows.
        GivenSummary(matchingCount: 0, totalCollected: 0);

        await _handler.Handle(Query(minimumPaid: 5m, maximumPaid: 20.50m), CancellationToken.None);

        SummaryParameter("MinimumPaid").Should().Be(5m);
        SummaryParameter("MaximumPaid").Should().Be(20.50m);
    }

    [Fact]
    public async Task Handle_ShouldApplyTheSameFilters_ToBothTheSummaryAndTheRows()
    {
        GivenSummary(matchingCount: 5, totalCollected: 0);
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        await _handler.Handle(Query(nameFilter: "Smith", acquiredFromUtc: from, minimumPaid: 5m), CancellationToken.None);

        foreach (var parameterName in new[] { "SeasonId", "NameFilter", "AcquiredFromUtc", "AcquiredBeforeUtc", "MinimumPaid", "MaximumPaid" })
        {
            PageParameter(parameterName).Should().Be(SummaryParameter(parameterName), $"{parameterName} must match");
        }
    }

    private static GetSeasonPassHoldersQuery Query(
        int seasonId = 7,
        int page = 1,
        int pageSize = PageSizes.Default,
        SeasonPassHolderSortField sortField = SeasonPassHolderSortField.AcquiredAt,
        SortDirection sortDirection = SortDirection.Ascending,
        string? nameFilter = null,
        DateTime? acquiredFromUtc = null,
        DateTime? acquiredBeforeUtc = null,
        decimal? minimumPaid = null,
        decimal? maximumPaid = null) =>
        new(seasonId, page, pageSize, sortField, sortDirection, nameFilter, acquiredFromUtc, acquiredBeforeUtc, minimumPaid, maximumPaid);

    private void GivenSummary(int matchingCount, decimal totalCollected, params SeasonPassHolderQueryResult[] rows)
    {
        _dbConnection
            .QuerySingleOrDefaultAsync<SeasonPassHoldersSummaryQueryResult>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<object?>())
            .Returns(new SeasonPassHoldersSummaryQueryResult("World Cup 2026", matchingCount, totalCollected));

        _dbConnection
            .QueryAsync<SeasonPassHolderQueryResult>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<object?>())
            .Returns(rows);
    }

    private static SeasonPassHolderQueryResult Row(
        string userId = "user-1",
        string fullName = "Antony Willson",
        string email = "antony@thepredictions.co.uk",
        SeasonPassTier tier = SeasonPassTier.Premium,
        SeasonPassSource source = SeasonPassSource.Purchased,
        decimal amountPaid = 12m,
        decimal smsFeePaid = 3m,
        DateTime? createdAtUtc = null) =>
        new(userId, fullName, email, tier, source, amountPaid, smsFeePaid,
            createdAtUtc ?? new DateTime(2026, 8, 4, 17, 42, 33, DateTimeKind.Utc));

    /// <summary>
    /// The SQL parameters are anonymous objects, so the assertions read them back by name off the
    /// recorded call rather than by declaring a type the handler deliberately keeps to itself.
    /// </summary>
    private object? SummaryParameter(string name) => ParameterValue(SummaryCallParameters(), name);

    private object? PageParameter(string name) => ParameterValue(PageCallParameters(), name);

    private object? SummaryCallParameters() => ParametersOfCallTo(nameof(IApplicationReadDbConnection.QuerySingleOrDefaultAsync));

    private object? PageCallParameters() => ParametersOfCallTo(nameof(IApplicationReadDbConnection.QueryAsync));

    private object? ParametersOfCallTo(string methodName) => _dbConnection
        .ReceivedCalls()
        .Where(call => call.GetMethodInfo().Name == methodName)
        .Select(call => call.GetArguments()[2])
        .FirstOrDefault();

    private static object? ParameterValue(object? parameters, string name) =>
        parameters?.GetType().GetProperty(name)?.GetValue(parameters);
}
