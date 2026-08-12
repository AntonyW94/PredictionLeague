using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's prize page.
/// </summary>
public class GetLeaguePrizesPageQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeaguePrizesPageQuery _prizesQuery = Substitute.For<ILeaguePrizesPageQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeaguePrizesPageQueryHandler _handler;

    public GetLeaguePrizesPageQueryHandlerTests()
    {
        _handler = new GetLeaguePrizesPageQueryHandler(_prizesQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _prizesQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _prizesQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((LeaguePrizesPageData?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheLeagueAndSeasonDetails()
    {
        // Arrange
        var deadline = SeasonStart.AddDays(-1);
        Given(Header(price: 15m, entryDeadlineUtc: deadline, numberOfRounds: 38));

        // Act
        var page = await HandleAsync();

        // Assert
        page.LeagueName.Should().Be("Test League");
        page.Price.Should().Be(15m);
        page.EntryDeadlineUtc.Should().Be(deadline);
        page.NumberOfRounds.Should().Be(38);
        page.SeasonStartDateUtc.Should().Be(SeasonStart);
    }

    [Fact]
    public async Task Handle_ShouldReportNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange - the column allows null even though the create and update commands both require a deadline, so this
        // is a state the database permits and the page must survive. The old result type declared it non-nullable and
        // would have failed to materialise.
        Given(Header(entryDeadlineUtc: null));

        // Act
        var page = await HandleAsync();

        // Assert - the page shows "Not set" rather than a date in 1900, and treats entry as closed, which is the same
        // answer the join and discovery screens give for a league with no deadline.
        page.EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCountEveryMembershipIncludingRequests()
    {
        // Arrange
        Given(Header(totalMembershipCount: 8, approvedMemberCount: 5));

        // Act
        var page = await HandleAsync();

        // Assert - preserved: the pot preview on this page counts pending and rejected requests, as the league settings
        // page does. Flagged in the plan document.
        page.MemberCount.Should().Be(8);
    }

    [Fact]
    public async Task Handle_ShouldListThePrizesAsConfigured()
    {
        // Arrange
        Given(
            Header(),
            new LeaguePrizeSettingRow(PrizeType.Overall, 1, 100m, null),
            new LeaguePrizeSettingRow(PrizeType.Monthly, 1, 25m, null),
            new LeaguePrizeSettingRow(PrizeType.Stages, 1, 40m, "Group Stage"));

        // Act
        var page = await HandleAsync();

        // Assert
        page.PrizeSettings.Should().HaveCount(3);
        page.PrizeSettings.Single(prize => prize.PrizeType == PrizeType.Stages).Stage.Should().Be("Group Stage");
        page.PrizeSettings.Single(prize => prize.PrizeType == PrizeType.Overall).PrizeAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNoPrizes_ForALeagueThatHasNotSetAnyUp()
    {
        // Arrange - the old left join returned a row of nulls for this, which is why every prize column was nullable.
        Given(Header());

        // Act
        var page = await HandleAsync();

        // Assert
        page.PrizeSettings.Should().BeEmpty();
        page.LeagueName.Should().Be("Test League");
    }

    private void Given(LeaguePrizesHeaderRow header, params LeaguePrizeSettingRow[] prizes)
    {
        _prizesQuery
            .ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new LeaguePrizesPageData(header, prizes));
    }

    private async Task<LeaguePrizesPageDto> HandleAsync() =>
        await _handler.Handle(new GetLeaguePrizesPageQuery(LeagueId, UserId), CancellationToken.None);

    private static LeaguePrizesHeaderRow Header(
        decimal price = 10m,
        DateTime? entryDeadlineUtc = null,
        int totalMembershipCount = 5,
        int approvedMemberCount = 5,
        int numberOfRounds = 38) =>
        new(
            "Test League",
            entryDeadlineUtc,
            price,
            totalMembershipCount,
            approvedMemberCount,
            numberOfRounds,
            SeasonStart,
            SeasonStart.AddMonths(9));
}
