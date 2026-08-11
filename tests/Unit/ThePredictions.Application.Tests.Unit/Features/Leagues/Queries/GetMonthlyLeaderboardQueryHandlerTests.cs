using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// One month's leaderboard for a league.
///
/// The shared rules - tie policy, totals, names, ordering - are the same as the overall table's and covered the
/// same way. What is specific here, and what most of these tests are about, is when a month's pre-round position
/// is worth showing. It is a stricter rule than the overall table's and looked similar enough in SQL to be
/// mistaken for it.
/// </summary>
public class GetMonthlyLeaderboardQueryHandlerTests
{
    private const int LeagueId = 42;
    private const int Month = 8;
    private const string ViewerId = "user-me";

    private readonly IMonthlyLeaderboardQuery _leaderboardQuery = Substitute.For<IMonthlyLeaderboardQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetMonthlyLeaderboardQueryHandler _handler;

    public GetMonthlyLeaderboardQueryHandlerTests()
    {
        _handler = new GetMonthlyLeaderboardQueryHandler(_leaderboardQuery, _membershipService);
    }

    #region The month's pre-round position

    [Fact]
    public async Task Handle_ShouldShowThePreRoundPosition_WhenAMonthRoundIsUnderWayAndAnotherHasStarted()
    {
        Given(
            members: [Member("u1", snapshotRank: 3)],
            monthRoundStatuses: [RoundStatus.Completed, RoundStatus.InProgress]);

        (await HandleAsync()).Single().SnapshotRank.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_DuringTheMonthsFirstRound()
    {
        // Only one of the month's rounds has started, so "the position before this round" is the position before
        // the month began - which is no position at all. This is the case the overall table's simpler rule would
        // get wrong if the two were merged.
        Given(
            members: [Member("u1", snapshotRank: 3)],
            monthRoundStatuses: [RoundStatus.InProgress, RoundStatus.Published]);

        (await HandleAsync()).Single().SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_WhenNoMonthRoundIsUnderWay()
    {
        // Two rounds finished but nothing live: there is no current round for an arrow to describe.
        Given(
            members: [Member("u1", snapshotRank: 3)],
            monthRoundStatuses: [RoundStatus.Completed, RoundStatus.Completed]);

        (await HandleAsync()).Single().SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_WhenTheMonthHasNoRoundsAtAll()
    {
        Given(members: [Member("u1", snapshotRank: 3)], monthRoundStatuses: []);

        (await HandleAsync()).Single().SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_WhenTheCacheHasNoneForThatMember()
    {
        Given(
            members: [Member("u1", snapshotRank: null)],
            monthRoundStatuses: [RoundStatus.Completed, RoundStatus.InProgress]);

        (await HandleAsync()).Single().SnapshotRank.Should().BeNull();
    }

    #endregion

    #region Totals, positions and names

    [Fact]
    public async Task Handle_ShouldRequireApprovedMembership_BeforeReadingAnything()
    {
        Given();

        await HandleAsync();

        await _membershipService.Received(1)
            .EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassTheLeagueAndMonthToThePort()
    {
        Given();

        await HandleAsync();

        await _leaderboardQuery.Received(1).ExecuteAsync(LeagueId, Month, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTotalOnlyTheMonthsPoints()
    {
        // The port already scopes the rows to the month, so the handler simply sums what it is given.
        Given(members: [Member("u1")], points: [Points("u1", 9), Points("u1", 12)]);

        (await HandleAsync()).Single().TotalPoints.Should().Be(21);
    }

    [Fact]
    public async Task Handle_ShouldScoreAMemberWithNoMonthPointsAsZero_RatherThanLeavingThemOut()
    {
        Given(members: [Member("u1"), Member("u2")], points: [Points("u1", 10)]);

        var entries = await HandleAsync();

        entries.Should().HaveCount(2);
        entries.Single(e => e.UserId == "u2").TotalPoints.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldShareAPositionAndLeaveAGap_WhenMembersAreTiedOnPoints()
    {
        Given(
            members: [Member("a", first: "Ann"), Member("b", first: "Bob"), Member("c", first: "Cat"), Member("d", first: "Dan")],
            points: [Points("a", 100), Points("b", 90), Points("c", 90), Points("d", 80)]);

        (await HandleAsync()).Select(e => e.Rank).Should().Equal(1, 2, 2, 4);
    }

    [Fact]
    public async Task Handle_ShouldOrderTiedMembersAlphabeticallyByFullName()
    {
        Given(
            members: [Member("z", first: "Zoe", last: "Zeta"), Member("a", first: "Ada", last: "Lovelace")],
            points: [Points("z", 10), Points("a", 10)]);

        (await HandleAsync()).Select(e => e.UserId).Should().Equal("a", "z");
    }

    [Fact]
    public async Task Handle_ShouldShowTheAbbreviatedNameOnScreen()
    {
        Given(members: [Member("u1", first: "Ada", last: "Lovelace")]);

        (await HandleAsync()).Single().PlayerName.Should().Be("Ada L");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldReportWhetherARoundIsInProgressAnywhereInTheSeason(bool inProgress)
    {
        // Season-wide, not month-scoped: it drives a banner rather than the month's arrow.
        Given(members: [Member("u1")], hasRoundInProgress: inProgress);

        (await HandleAsync()).Single().IsRoundInProgress.Should().Be(inProgress);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenTheLeagueHasNoApprovedMembers()
    {
        Given(members: []);

        (await HandleAsync()).Should().BeEmpty();
    }

    #endregion

    private void Given(
        IReadOnlyList<LeaderboardMemberRow>? members = null,
        IReadOnlyList<MemberRoundPointsRow>? points = null,
        IReadOnlyList<RoundStatus>? monthRoundStatuses = null,
        bool hasRoundInProgress = false) =>
        _leaderboardQuery.ExecuteAsync(LeagueId, Month, Arg.Any<CancellationToken>())
            .Returns(new MonthlyLeaderboardData(
                members ?? [Member("u1")],
                points ?? [],
                monthRoundStatuses ?? [RoundStatus.Completed, RoundStatus.InProgress],
                hasRoundInProgress));

    private static LeaderboardMemberRow Member(
        string userId, int? snapshotRank = null, string first = "Ada", string last = "Lovelace") =>
        new(userId, first, last, snapshotRank);

    private static MemberRoundPointsRow Points(string userId, int boostedPoints) => new(userId, boostedPoints);

    private async Task<IReadOnlyList<Contracts.Leaderboards.LeaderboardEntryDto>> HandleAsync() =>
        (await _handler.Handle(new GetMonthlyLeaderboardQuery(LeagueId, Month, ViewerId), CancellationToken.None)).ToList();
}
