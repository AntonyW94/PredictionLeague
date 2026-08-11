using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// What an administrator has waiting for them.
///
/// The rule that decides which of their leagues count as still taking entries is one tick different from the one the
/// league-discovery queries use, which is the thing most worth pinning here.
/// </summary>
public class GetPendingMembersForAdminQueryHandlerTests
{
    private const string UserId = "user-admin";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Joined = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAdminPendingMembersQuery _pendingMembersQuery = Substitute.For<IAdminPendingMembersQuery>();
    private readonly GetPendingMembersForAdminQueryHandler _handler;

    public GetPendingMembersForAdminQueryHandlerTests()
    {
        _handler = new GetPendingMembersForAdminQueryHandler(_pendingMembersQuery, new TestDateTimeProvider(Now));
    }

    #region Which leagues count

    [Fact]
    public async Task Handle_ShouldReportNoOpenLeague_WhenTheAdministratorRunsNone()
    {
        // Arrange
        Given();

        // Act
        var result = await HandleAsync();

        // Assert
        result.IsAdminOfOpenLeague.Should().BeFalse();
        result.AdminLeagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportNoOpenLeague_WhenEveryLeagueHasClosed()
    {
        // Arrange
        Given(leagues: [League(1, entryDeadlineUtc: Now.AddSeconds(-1))]);

        // Act
        var result = await HandleAsync();

        // Assert
        result.IsAdminOfOpenLeague.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCountALeagueAsOpen_AtTheDeadlineItself()
    {
        // Arrange
        Given(leagues: [League(1, entryDeadlineUtc: Now)]);

        // Act
        var result = await HandleAsync();

        // Assert - this rule is inclusive, while the league-discovery rule is not: at this exact instant the administrator
        // still sees the league but a player can no longer join it. One tick apart, and flagged in the plan document.
        result.IsAdminOfOpenLeague.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotCountALeagueWithNoDeadlineAtAll()
    {
        // Arrange
        Given(leagues: [League(1) with { EntryDeadlineUtc = null }]);

        // Act
        var result = await HandleAsync();

        // Assert - which the old SQL comparison did through its treatment of nulls rather than by saying so.
        result.IsAdminOfOpenLeague.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldOrderTheLeaguesByName()
    {
        // Arrange
        Given(leagues: [League(1, name: "Zebras"), League(2, name: "Aardvarks")]);

        // Act
        var result = await HandleAsync();

        // Assert
        result.AdminLeagues.Select(league => league.LeagueName).Should().Equal("Aardvarks", "Zebras");
    }

    [Fact]
    public async Task Handle_ShouldCarryEachLeaguesCountsAndSettings()
    {
        // Arrange
        Given(leagues:
        [
            League(1, memberCount: 8, pendingCount: 2, price: 10m, isFree: false, entryCode: "SECRET")
        ]);

        // Act
        var league = (await HandleAsync()).AdminLeagues.Single();

        // Assert - the entry code is the administrator's own, so it is theirs to see.
        league.MemberCount.Should().Be(8);
        league.PendingCount.Should().Be(2);
        league.Price.Should().Be(10m);
        league.IsFree.Should().BeFalse();
        league.EntryCode.Should().Be("SECRET");
    }

    #endregion

    #region Which requests are listed

    [Fact]
    public async Task Handle_ShouldListTheRequestsWaitingOnAnOpenLeague()
    {
        // Arrange
        Given(
            leagues: [League(1)],
            members: [Member(1, "u1", "Grace", "Hopper")]);

        // Act
        var result = await HandleAsync();

        // Assert
        result.Members.Single().MemberName.Should().Be("Grace H");
        result.Members.Single().UserId.Should().Be("u1");
    }

    [Fact]
    public async Task Handle_ShouldNotListARequestForALeagueThatHasClosed()
    {
        // Arrange - one open league, one closed, a request waiting on each.
        Given(
            leagues: [League(1), League(2, entryDeadlineUtc: Now.AddSeconds(-1))],
            members: [Member(1, "u1", "Grace", "Hopper"), Member(2, "u2", "Alan", "Turing")]);

        // Act
        var result = await HandleAsync();

        // Assert - there is nothing useful an administrator can do about a league nobody can still join.
        result.Members.Select(member => member.LeagueId).Should().Equal(1);
    }

    [Fact]
    public async Task Handle_ShouldOrderRequestsByLeagueThenByWhenTheyWereMade()
    {
        // Arrange
        Given(
            leagues: [League(1, name: "Zebras"), League(2, name: "Aardvarks")],
            members:
            [
                Member(1, "u1", "Grace", "Hopper", joinedAtUtc: Joined, leagueName: "Zebras"),
                Member(2, "u2", "Alan", "Turing", joinedAtUtc: Joined.AddDays(1), leagueName: "Aardvarks"),
                Member(2, "u3", "Ada", "Lovelace", joinedAtUtc: Joined, leagueName: "Aardvarks")
            ]);

        // Act
        var result = await HandleAsync();

        // Assert - grouped by league, and oldest request first within each so nobody is left waiting longest.
        result.Members.Select(member => member.UserId).Should().Equal("u3", "u2", "u1");
    }

    [Fact]
    public async Task Handle_ShouldListNoRequests_WhenNobodyIsWaiting()
    {
        // Arrange
        Given(leagues: [League(1)]);

        // Act
        var result = await HandleAsync();

        // Assert - still an administrator of an open league, just with nothing to decide.
        result.IsAdminOfOpenLeague.Should().BeTrue();
        result.Members.Should().BeEmpty();
    }

    #endregion

    private void Given(
        IReadOnlyList<AdministeredLeagueRow>? leagues = null,
        IReadOnlyList<PendingMemberRow>? members = null)
    {
        _pendingMembersQuery
            .ExecuteAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new AdminPendingMembersData(leagues ?? [], members ?? []));
    }

    private async Task<PendingMembersResultDto> HandleAsync() =>
        await _handler.Handle(new GetPendingMembersForAdminQuery(UserId), CancellationToken.None);

    /// <summary>
    /// An open league unless a test says otherwise. For a league with no deadline use
    /// <c>League(...) with { EntryDeadlineUtc = null }</c>.
    /// </summary>
    private static AdministeredLeagueRow League(
        int leagueId,
        string? name = null,
        DateTime? entryDeadlineUtc = null,
        int memberCount = 5,
        int pendingCount = 0,
        decimal price = 10m,
        bool isFree = false,
        string? entryCode = null) =>
        new(
            leagueId,
            name ?? $"League {leagueId}",
            entryDeadlineUtc ?? Now.AddDays(7),
            memberCount,
            pendingCount,
            price,
            isFree,
            entryCode);

    private static PendingMemberRow Member(
        int leagueId,
        string userId,
        string firstName,
        string lastName,
        DateTime? joinedAtUtc = null,
        string? leagueName = null) =>
        new(
            leagueId,
            leagueName ?? $"League {leagueId}",
            userId,
            firstName,
            lastName,
            joinedAtUtc ?? Joined);
}
