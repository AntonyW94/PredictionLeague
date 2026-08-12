using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The player's own outstanding league requests.
///
/// The rule worth testing is which ones stop being shown: a rejection follows somebody around until they dismiss the notice,
/// and then stops.
/// </summary>
public class GetPendingRequestsQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime Joined = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMyLeagueRequestsQuery _requestsQuery = Substitute.For<IMyLeagueRequestsQuery>();
    private readonly GetPendingRequestsQueryHandler _handler;

    public GetPendingRequestsQueryHandlerTests()
    {
        _handler = new GetPendingRequestsQueryHandler(_requestsQuery);
    }

    #region Which requests are shown

    [Fact]
    public async Task Handle_ShouldShowARequestStillWaitingForAnAnswer()
    {
        // Arrange
        Given(Request(1, LeagueMemberStatus.Pending));

        // Act
        var requests = await HandleAsync();

        // Assert
        requests.Select(request => request.LeagueId).Should().Equal(1);
    }

    [Fact]
    public async Task Handle_ShouldShowARejectionTheePlayerHasNotDismissed()
    {
        // Arrange
        Given(Request(1, LeagueMemberStatus.Rejected, isAlertDismissed: false));

        // Act
        var requests = await HandleAsync();

        // Assert
        requests.Single().Status.Should().Be(LeagueMemberStatus.Rejected);
    }

    [Fact]
    public async Task Handle_ShouldStopShowingARejectionOnceItHasBeenDismissed()
    {
        // Arrange
        Given(Request(1, LeagueMemberStatus.Rejected, isAlertDismissed: true));

        // Act
        var requests = await HandleAsync();

        // Assert - the dismissal is what stops a rejection following somebody around for ever.
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldStillShowAPendingRequestMarkedAsDismissed()
    {
        // Arrange - not a state the application produces, since there is no notice to dismiss while still waiting.
        Given(Request(1, LeagueMemberStatus.Pending, isAlertDismissed: true));

        // Act
        var requests = await HandleAsync();

        // Assert - the dismissal only ever applied to rejections, and preserving that keeps a waiting player informed.
        requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldNotShowAnApprovedMembership()
    {
        // Arrange - the port filters these out, so this guards against the handler letting one through.
        Given(Request(1, LeagueMemberStatus.Approved));

        // Act
        var requests = await HandleAsync();

        // Assert
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldShowNothing_WhenThePlayerHasMadeNoRequests()
    {
        // Arrange
        Given();

        // Act
        var requests = await HandleAsync();

        // Assert
        requests.Should().BeEmpty();
    }

    #endregion

    #region What each request shows

    [Fact]
    public async Task Handle_ShouldOrderTheNewestRequestFirst()
    {
        // Arrange
        Given(
            Request(1, LeagueMemberStatus.Pending, joinedAtUtc: Joined),
            Request(2, LeagueMemberStatus.Pending, joinedAtUtc: Joined.AddDays(1)));

        // Act
        var requests = await HandleAsync();

        // Assert
        requests.Select(request => request.LeagueId).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldWorkOutThePotFromTheEntryFeesAndTheTopUp()
    {
        // Arrange
        Given(Request(1, LeagueMemberStatus.Pending, price: 10m, memberCount: 12, prizeFundOverride: 50m));

        // Act
        var request = (await HandleAsync()).Single();

        // Assert
        request.PotValue.Should().Be(170m);
        request.EntryFee.Should().Be(10m);
        request.MemberCount.Should().Be(12);
    }

    [Fact]
    public async Task Handle_ShouldAbbreviateTheAdministratorsName()
    {
        // Arrange
        Given(Request(1, LeagueMemberStatus.Pending));

        // Act
        var request = (await HandleAsync()).Single();

        // Assert
        request.AdminName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheLeagueAndSeasonNames()
    {
        // Arrange
        Given(Request(1, LeagueMemberStatus.Pending));

        // Act
        var request = (await HandleAsync()).Single();

        // Assert
        request.LeagueName.Should().Be("Test League");
        request.SeasonName.Should().Be("2026/27");
        request.JoinedAtUtc.Should().Be(Joined);
    }

    [Fact]
    public async Task Handle_ShouldReportNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange - this read has no deadline filter, so the old non-nullable result type would have failed to materialise
        // and taken the whole dashboard down.
        Given(Request(1, LeagueMemberStatus.Pending) with { EntryDeadlineUtc = null });

        // Act
        var request = (await HandleAsync()).Single();

        // Assert
        // A date in 1900 could be formatted and sorted as though somebody had chosen it. The tile now says so instead.
        request.EntryDeadlineUtc.Should().BeNull();
    }

    #endregion

    private void Given(params MyLeagueRequestRow[] requests)
    {
        _requestsQuery.ExecuteAsync(UserId, Arg.Any<CancellationToken>()).Returns(requests);
    }

    private async Task<IEnumerable<LeagueRequestDto>> HandleAsync() =>
        await _handler.Handle(new GetPendingRequestsQuery(UserId), CancellationToken.None);

    /// <summary>
    /// A request with a deadline unless a test says otherwise. For a league with no deadline use
    /// <c>Request(...) with { EntryDeadlineUtc = null }</c>, which says so plainly.
    /// </summary>
    private static MyLeagueRequestRow Request(
        int leagueId,
        LeagueMemberStatus status,
        bool isAlertDismissed = false,
        DateTime? joinedAtUtc = null,
        decimal price = 10m,
        int memberCount = 5,
        decimal? prizeFundOverride = null) =>
        new(
            leagueId,
            "Test League",
            "2026/27",
            status,
            isAlertDismissed,
            joinedAtUtc ?? Joined,
            Joined.AddDays(7),
            "Ada",
            "Lovelace",
            memberCount,
            price,
            prizeFundOverride);
}
