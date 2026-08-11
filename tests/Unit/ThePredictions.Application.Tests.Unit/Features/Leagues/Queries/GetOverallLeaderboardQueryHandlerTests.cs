using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's overall leaderboard. The first of nine handlers to adopt <c>Domain.Services.Ranking</c>.
///
/// This handler was excluded from coverage while its five rules lived in SQL. Every test below asserts
/// something that previously needed a database to observe - and the tie tests in particular assert the thing
/// that would silently renumber the table if it regressed.
/// </summary>
public class GetOverallLeaderboardQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string ViewerId = "user-me";

    private readonly IOverallLeaderboardQuery _leaderboardQuery = Substitute.For<IOverallLeaderboardQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetOverallLeaderboardQueryHandler _handler;

    public GetOverallLeaderboardQueryHandlerTests()
    {
        _handler = new GetOverallLeaderboardQueryHandler(_leaderboardQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldRequireApprovedMembership_BeforeReadingAnything()
    {
        Given();

        await HandleAsync();

        await _membershipService.Received(1)
            .EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTotalEachMembersRoundPoints()
    {
        Given(
            members: [Member("u1", "Ada", "Lovelace")],
            points: [Points("u1", 9), Points("u1", 12), Points("u1", 4)]);

        (await HandleAsync()).Single().TotalPoints.Should().Be(25);
    }

    [Fact]
    public async Task Handle_ShouldScoreAMemberWithNoResultsAsZero_RatherThanLeavingThemOut()
    {
        // The old SQL wrapped the sum in COALESCE(..., 0) precisely so a member who has played nothing still
        // appears, at the bottom.
        Given(
            members: [Member("u1", "Ada", "Lovelace"), Member("u2", "Grace", "Hopper")],
            points: [Points("u1", 10)]);

        var entries = await HandleAsync();

        entries.Should().HaveCount(2);
        entries.Single(e => e.UserId == "u2").TotalPoints.Should().Be(0);
        entries.Single(e => e.UserId == "u2").Rank.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldOrderByPointsDescending()
    {
        Given(
            members: [Member("low", "Ada", "Lovelace"), Member("high", "Grace", "Hopper")],
            points: [Points("low", 5), Points("high", 50)]);

        (await HandleAsync()).Select(e => e.UserId).Should().Equal("high", "low");
    }

    [Fact]
    public async Task Handle_ShouldShareAPositionAndLeaveAGap_WhenMembersAreTiedOnPoints()
    {
        // The tie policy, now applied here rather than by RANK(). Nobody is third.
        Given(
            members: [Member("a", "Ann", "Alpha"), Member("b", "Bob", "Beta"), Member("c", "Cat", "Gamma"), Member("d", "Dan", "Delta")],
            points: [Points("a", 100), Points("b", 90), Points("c", 90), Points("d", 80)]);

        (await HandleAsync()).Select(e => e.Rank).Should().Equal(1, 2, 2, 4);
    }

    [Fact]
    public async Task Handle_ShouldOrderTiedMembersAlphabeticallyByFullName()
    {
        Given(
            members: [Member("z", "Zoe", "Zeta"), Member("a", "Ada", "Lovelace"), Member("g", "Grace", "Hopper")],
            points: [Points("z", 10), Points("a", 10), Points("g", 10)]);

        (await HandleAsync()).Select(e => e.UserId).Should().Equal("a", "g", "z");
    }

    [Fact]
    public async Task Handle_ShouldOrderTiedMembersBySurname_WhenTheirDisplayNamesAreIdentical()
    {
        // Both display as "Ada L", so only the full name can separate them - which is why the tie-break uses it.
        Given(
            members: [Member("lovelace", "Ada", "Lovelace"), Member("lamarr", "Ada", "Lamarr")],
            points: [Points("lovelace", 10), Points("lamarr", 10)]);

        var entries = await HandleAsync();

        entries.Select(e => e.UserId).Should().Equal("lamarr", "lovelace");
        entries.Select(e => e.PlayerName).Should().Equal("Ada L", "Ada L");
    }

    [Fact]
    public async Task Handle_ShouldShowTheAbbreviatedNameOnScreen()
    {
        Given(members: [Member("u1", "Ada", "Lovelace")]);

        (await HandleAsync()).Single().PlayerName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldShowThePreRoundPosition_OnceARoundHasBeenCompleted()
    {
        Given(members: [Member("u1", "Ada", "Lovelace", snapshotRank: 3)], hasCompletedRound: true);

        (await HandleAsync()).Single().SnapshotRank.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_BeforeAnyRoundHasBeenCompleted()
    {
        // Nothing to have moved from yet, so the arrow is hidden rather than drawn against a meaningless value.
        Given(members: [Member("u1", "Ada", "Lovelace", snapshotRank: 3)], hasCompletedRound: false);

        (await HandleAsync()).Single().SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportNoPreRoundPosition_WhenTheCacheHasNoneForThatMember()
    {
        Given(members: [Member("u1", "Ada", "Lovelace", snapshotRank: null)], hasCompletedRound: true);

        (await HandleAsync()).Single().SnapshotRank.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldReportWhetherARoundIsInProgress(bool inProgress)
    {
        Given(members: [Member("u1", "Ada", "Lovelace")], hasRoundInProgress: inProgress);

        (await HandleAsync()).Single().IsRoundInProgress.Should().Be(inProgress);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenTheLeagueHasNoApprovedMembers()
    {
        Given(members: []);

        (await HandleAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIgnorePointsForSomeoneNoLongerAMember()
    {
        // Results outlive membership, so a departed member's rows must not create a phantom entry.
        Given(members: [Member("u1", "Ada", "Lovelace")], points: [Points("u1", 10), Points("departed", 999)]);

        (await HandleAsync()).Select(e => e.UserId).Should().Equal("u1");
    }

    private void Given(
        IReadOnlyList<LeaderboardMemberRow>? members = null,
        IReadOnlyList<MemberRoundPointsRow>? points = null,
        bool hasCompletedRound = true,
        bool hasRoundInProgress = false) =>
        _leaderboardQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new OverallLeaderboardData(
                members ?? [Member("u1", "Ada", "Lovelace")],
                points ?? [],
                hasCompletedRound,
                hasRoundInProgress));

    private static LeaderboardMemberRow Member(
        string userId, string firstName, string lastName, int? snapshotRank = null) =>
        new(userId, firstName, lastName, snapshotRank);

    private static MemberRoundPointsRow Points(string userId, int boostedPoints) => new(userId, boostedPoints);

    private async Task<IReadOnlyList<Contracts.Leaderboards.LeaderboardEntryDto>> HandleAsync() =>
        (await _handler.Handle(new GetOverallLeaderboardQuery(LeagueId, ViewerId), CancellationToken.None)).ToList();
}
