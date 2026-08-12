using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Leaderboards;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The dashboard's leaderboards tile - several leagues at once, each with its own small table.
///
/// Seven rules came out of one windowed CTE, and the tile's ordering was already stated twice in the handler
/// itself: once as SQL and once as LINQ over the same rows.
/// </summary>
public class GetLeaderboardsQueryHandlerTests
{
    private const string ViewerId = "user-me";

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IDashboardLeaderboardsQuery _leaderboardsQuery = Substitute.For<IDashboardLeaderboardsQuery>();
    private readonly GetLeaderboardsQueryHandler _handler;

    public GetLeaderboardsQueryHandlerTests()
    {
        _handler = new GetLeaderboardsQueryHandler(_leaderboardsQuery);
    }

    #region Ranking

    [Fact]
    public async Task Handle_ShouldRankEachLeagueSeparately()
    {
        // Arrange - the same two players in two leagues, scoring the opposite way round.
        Given(
            leagues: [League(1), League(2)],
            members:
            [
                Member(1, "u1", "Ada", "Lovelace"), Member(1, "u2", "Grace", "Hopper"),
                Member(2, "u1", "Ada", "Lovelace"), Member(2, "u2", "Grace", "Hopper")
            ],
            points:
            [
                Points(1, "u1", 20), Points(1, "u2", 5),
                Points(2, "u1", 5), Points(2, "u2", 20)
            ]);

        // Act
        var leagues = (await HandleAsync()).ToList();

        // Assert - the old RANK() was partitioned by league; a single ranking would have been wrong for one of them.
        leagues.Single(league => league.LeagueId == 1).Entries.First().UserId.Should().Be("u1");
        leagues.Single(league => league.LeagueId == 2).Entries.First().UserId.Should().Be("u2");
    }

    [Fact]
    public async Task Handle_ShouldTotalEveryRoundsPoints()
    {
        // Arrange
        Given(
            leagues: [League(1)],
            members: [Member(1, "u1", "Ada", "Lovelace")],
            points: [Points(1, "u1", 12), Points(1, "u1", 7), Points(1, "u1", 3)]);

        // Act
        var entry = (await HandleAsync()).Single().Entries.Single();

        // Assert
        entry.TotalPoints.Should().Be(22);
    }

    [Fact]
    public async Task Handle_ShouldScoreAMemberWithNoResultsAsZero()
    {
        // Arrange
        Given(
            leagues: [League(1)],
            members: [Member(1, "u1", "Ada", "Lovelace"), Member(1, "u2", "Grace", "Hopper")],
            points: [Points(1, "u1", 12)]);

        // Act
        var entries = (await HandleAsync()).Single().Entries.ToList();

        // Assert - the old SUM(ISNULL(...)) over a left join kept them in the table.
        entries.Single(entry => entry.UserId == "u2").TotalPoints.Should().Be(0);
        entries.Single(entry => entry.UserId == "u2").Rank.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldGiveJointPositionsTheSameRankAndSkipTheNext()
    {
        // Arrange
        Given(
            leagues: [League(1)],
            members:
            [
                Member(1, "u1", "Ada", "Lovelace"),
                Member(1, "u2", "Grace", "Hopper"),
                Member(1, "u3", "Alan", "Turing")
            ],
            points: [Points(1, "u1", 20), Points(1, "u2", 20), Points(1, "u3", 5)]);

        // Act
        var entries = (await HandleAsync()).Single().Entries.ToList();

        // Assert
        entries.Select(entry => entry.Rank).Should().Equal(1, 1, 3);
    }

    [Fact]
    public async Task Handle_ShouldOrderJointPositionsByFullName()
    {
        // Arrange
        Given(
            leagues: [League(1)],
            members: [Member(1, "u2", "Grace", "Hopper"), Member(1, "u1", "Ada", "Lovelace")],
            points: [Points(1, "u1", 20), Points(1, "u2", 20)]);

        // Act
        var entries = (await HandleAsync()).Single().Entries.ToList();

        // Assert
        entries.Select(entry => entry.PlayerName).Should().Equal("Ada L", "Grace H");
    }

    [Fact]
    public async Task Handle_ShouldAbbreviateThePlayerName()
    {
        // Arrange
        Given(
            leagues: [League(1)],
            members: [Member(1, "u1", "Ada", "Lovelace")],
            points: []);

        // Act
        var entry = (await HandleAsync()).Single().Entries.Single();

        // Assert
        entry.PlayerName.Should().Be("Ada L");
    }

    #endregion

    #region The rank-change arrow

    [Fact]
    public async Task Handle_ShouldShowTheCachedPositionOnceARoundHasFinished()
    {
        // Arrange
        Given(
            leagues: [League(1, completedRoundCount: 3)],
            members: [Member(1, "u1", "Ada", "Lovelace", snapshotRank: 4)],
            points: []);

        // Act
        var entry = (await HandleAsync()).Single().Entries.Single();

        // Assert
        entry.SnapshotRank.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldHideTheCachedPosition_BeforeAnyRoundHasFinished()
    {
        // Arrange
        Given(
            leagues: [League(1, completedRoundCount: 0)],
            members: [Member(1, "u1", "Ada", "Lovelace", snapshotRank: 4)],
            points: []);

        // Act
        var entry = (await HandleAsync()).Single().Entries.Single();

        // Assert - no earlier position exists to have moved from.
        entry.SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportWhetherARoundIsUnderWay()
    {
        // Arrange
        Given(
            leagues: [League(1, hasRoundInProgress: true)],
            members: [Member(1, "u1", "Ada", "Lovelace")],
            points: []);

        // Act
        var entry = (await HandleAsync()).Single().Entries.Single();

        // Assert
        entry.IsRoundInProgress.Should().BeTrue();
    }

    #endregion

    #region Whether a league is finished

    [Fact]
    public async Task Handle_ShouldReportTheLeagueAsFinished_WhenEveryRoundHasCompleted()
    {
        // Arrange
        Given(
            leagues: [League(1, seasonRoundCount: 3, completedRoundCount: 3)],
            members: [Member(1, "u1", "Ada", "Lovelace")],
            points: []);

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportTheLeagueAsUnfinished_WhileRoundsRemain()
    {
        // Arrange
        Given(
            leagues: [League(1, seasonRoundCount: 3, completedRoundCount: 2)],
            members: [Member(1, "u1", "Ada", "Lovelace")],
            points: []);

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheArchiveFlagForTheViewer()
    {
        // Arrange
        Given(
            leagues: [League(1, isArchivedByUser: true)],
            members: [Member(1, "u1", "Ada", "Lovelace")],
            points: []);

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.IsArchivedByUser.Should().BeTrue();
    }

    #endregion

    #region The order the tiles appear in

    [Fact]
    public async Task Handle_ShouldPutALeagueWithARoundUnderWayFirst()
    {
        // Arrange - the live league started its season later, so only the in-progress rule can lift it.
        Given(
            leagues:
            [
                League(1, seasonStartDateUtc: SeasonStart),
                League(2, seasonStartDateUtc: SeasonStart.AddMonths(1), hasRoundInProgress: true)
            ],
            members: [],
            points: []);

        // Act
        var leagues = (await HandleAsync()).ToList();

        // Assert
        leagues.Select(league => league.LeagueId).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldThenOrderByWhenTheSeasonStarted()
    {
        // Arrange
        Given(
            leagues:
            [
                League(1, seasonStartDateUtc: SeasonStart.AddMonths(1)),
                League(2, seasonStartDateUtc: SeasonStart)
            ],
            members: [],
            points: []);

        // Act
        var leagues = (await HandleAsync()).ToList();

        // Assert
        leagues.Select(league => league.LeagueId).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldThenPutTheHigherStakeLeagueFirst()
    {
        // Arrange
        Given(
            leagues: [League(1, price: 5m), League(2, price: 20m)],
            members: [],
            points: []);

        // Act
        var leagues = (await HandleAsync()).ToList();

        // Assert - the league a player has most at risk sits above the free one they joined for fun.
        leagues.Select(league => league.LeagueId).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldThenOrderByName()
    {
        // Arrange
        Given(
            leagues: [League(1, name: "Zebras"), League(2, name: "Aardvarks")],
            members: [],
            points: []);

        // Act
        var leagues = (await HandleAsync()).ToList();

        // Assert
        leagues.Select(league => league.LeagueName).Should().Equal("Aardvarks", "Zebras");
    }

    #endregion

    #region Nothing to show

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThePlayerIsInNoLeagues()
    {
        // Arrange
        Given(leagues: [], members: [], points: []);

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyTable_ForALeagueWithNoMembersOfItsOwn()
    {
        // Arrange - a league row with no member rows should not throw.
        Given(leagues: [League(1)], members: [], points: []);

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.Entries.Should().BeEmpty();
    }

    #endregion

    private void Given(
        IReadOnlyList<DashboardLeagueRow> leagues,
        IReadOnlyList<DashboardLeagueMemberRow> members,
        IReadOnlyList<DashboardLeagueMemberPointsRow> points)
    {
        _leaderboardsQuery
            .ExecuteAsync(ViewerId, Arg.Any<CancellationToken>())
            .Returns(new DashboardLeaderboardsData(leagues, members, points));
    }

    private async Task<IEnumerable<LeagueLeaderboardDto>> HandleAsync() =>
        await _handler.Handle(new GetLeaderboardsQuery(ViewerId), CancellationToken.None);

    private static DashboardLeagueRow League(
        int leagueId,
        string? name = null,
        decimal price = 10m,
        DateTime? seasonStartDateUtc = null,
        int seasonRoundCount = 38,
        int completedRoundCount = 1,
        bool hasRoundInProgress = false,
        bool isArchivedByUser = false) =>
        new(
            leagueId,
            name ?? $"League {leagueId}",
            price,
            "2026/27",
            seasonStartDateUtc ?? SeasonStart,
            seasonRoundCount,
            completedRoundCount,
            hasRoundInProgress,
            isArchivedByUser);

    private static DashboardLeagueMemberRow Member(
        int leagueId,
        string userId,
        string firstName,
        string lastName,
        int? snapshotRank = null) =>
        new(leagueId, userId, firstName, lastName, snapshotRank);

    private static DashboardLeagueMemberPointsRow Points(int leagueId, string userId, int boostedPoints) =>
        new(leagueId, userId, boostedPoints);
}
