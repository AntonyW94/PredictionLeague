using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A tournament stage's leaderboard - the richest of the nine, with seven rules out of one statement including
/// two ranks.
///
/// The pre-round position is computed here rather than read from a cache, so unlike the overall and monthly
/// tables its value is testable and not merely its visibility. That is what most of these tests are about.
/// </summary>
public class GetStageLeaderboardQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string ViewerId = "user-me";

    private readonly IStageLeaderboardQuery _leaderboardQuery = Substitute.For<IStageLeaderboardQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetStageLeaderboardQueryHandler _handler;

    public GetStageLeaderboardQueryHandlerTests()
    {
        _handler = new GetStageLeaderboardQueryHandler(_leaderboardQuery, _membershipService);
    }

    #region Stage classification

    [Fact]
    public async Task Handle_ShouldCountOnlyTheRequestedStagesRounds()
    {
        // Round 1 is a group round, round 2 knockout. Asking for the group stage must ignore round 2's points.
        Given(
            rounds: [GroupRound(1), KnockoutRound(2)],
            points: [Points("u1", 1, 10), Points("u1", 2, 500)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().TotalPoints.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyTheKnockoutRounds_WhenAskedForTheKnockoutStage()
    {
        Given(
            rounds: [GroupRound(1), KnockoutRound(2)],
            points: [Points("u1", 1, 10), Points("u1", 2, 500)]);

        (await HandleAsync(TournamentStageGroup.KnockoutStage)).Single().TotalPoints.Should().Be(500);
    }

    [Fact]
    public async Task Handle_ShouldTreatARoundWithNoTournamentMappingAsKnockout()
    {
        // The old CASE had no null arm, so an unmapped round fell to its ELSE. Preserved.
        Given(
            rounds: [new SeasonRoundStageRow(1, null, RoundStatus.Completed)],
            points: [Points("u1", 1, 7)]);

        (await HandleAsync(TournamentStageGroup.KnockoutStage)).Single().TotalPoints.Should().Be(7);
    }

    #endregion

    #region The computed pre-round position

    [Fact]
    public async Task Handle_ShouldComputeThePreRoundPositionExcludingTheRoundInProgress()
    {
        // Before round 2 began, u2 led on 20 to u1's 10. Round 2 flips the current table but must not flip the
        // position they are being compared against.
        Given(
            members: [Member("u1", "Ann", "Alpha"), Member("u2", "Bob", "Beta")],
            rounds: [GroupRound(1, RoundStatus.Completed), GroupRound(2, RoundStatus.InProgress)],
            points:
            [
                Points("u1", 1, 10), Points("u1", 2, 100),
                Points("u2", 1, 20), Points("u2", 2, 0)
            ]);

        var entries = await HandleAsync(TournamentStageGroup.GroupStage);

        entries.Single(e => e.UserId == "u1").Rank.Should().Be(1, "110 now leads.");
        entries.Single(e => e.UserId == "u1").SnapshotRank.Should().Be(2, "but u1 was second before round 2.");
        entries.Single(e => e.UserId == "u2").SnapshotRank.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldApplyTheTiePolicyToThePreRoundPositionToo()
    {
        Given(
            members: [Member("a", "Ann", "Alpha"), Member("b", "Bob", "Beta"), Member("c", "Cat", "Gamma")],
            rounds: [GroupRound(1, RoundStatus.Completed), GroupRound(2, RoundStatus.InProgress)],
            points: [Points("a", 1, 10), Points("b", 1, 10), Points("c", 1, 5)]);

        var entries = await HandleAsync(TournamentStageGroup.GroupStage);

        entries.Select(e => e.SnapshotRank).Should().Equal(1, 1, 3);
    }

    [Fact]
    public async Task Handle_ShouldShowThePreRoundPosition_WhenAStageRoundIsUnderWayAndAnotherHasStarted()
    {
        Given(rounds: [GroupRound(1, RoundStatus.Completed), GroupRound(2, RoundStatus.InProgress)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().SnapshotRank.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_DuringTheStagesFirstRound()
    {
        Given(rounds: [GroupRound(1, RoundStatus.InProgress), GroupRound(2, RoundStatus.Published)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_WhenNoStageRoundIsUnderWay()
    {
        Given(rounds: [GroupRound(1, RoundStatus.Completed), GroupRound(2, RoundStatus.Completed)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().SnapshotRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldHideThePreRoundPosition_WhenTheLiveRoundIsInTheOtherStage()
    {
        // A knockout round under way says nothing about the group stage's table.
        Given(rounds: [GroupRound(1, RoundStatus.Completed), GroupRound(2, RoundStatus.Completed), KnockoutRound(3, RoundStatus.InProgress)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().SnapshotRank.Should().BeNull();
    }

    #endregion

    #region The current table

    [Fact]
    public async Task Handle_ShouldRequireApprovedMembership_BeforeReadingAnything()
    {
        Given();

        await HandleAsync(TournamentStageGroup.GroupStage);

        await _membershipService.Received(1)
            .EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldShareAPositionAndLeaveAGap_WhenMembersAreTied()
    {
        Given(
            members: [Member("a", "Ann", "Alpha"), Member("b", "Bob", "Beta"), Member("c", "Cat", "Gamma"), Member("d", "Dan", "Delta")],
            rounds: [GroupRound(1, RoundStatus.Completed)],
            points: [Points("a", 1, 100), Points("b", 1, 90), Points("c", 1, 90), Points("d", 1, 80)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Select(e => e.Rank).Should().Equal(1, 2, 2, 4);
    }

    [Fact]
    public async Task Handle_ShouldOrderTiedMembersAlphabeticallyByFullName()
    {
        Given(
            members: [Member("z", "Zoe", "Zeta"), Member("a", "Ada", "Lovelace")],
            rounds: [GroupRound(1, RoundStatus.Completed)],
            points: [Points("z", 1, 5), Points("a", 1, 5)]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Select(e => e.UserId).Should().Equal("a", "z");
    }

    [Fact]
    public async Task Handle_ShouldScoreAMemberWithNoStagePointsAsZero()
    {
        Given(
            members: [Member("u1", "Ada", "Lovelace"), Member("u2", "Grace", "Hopper")],
            rounds: [GroupRound(1, RoundStatus.Completed)],
            points: [Points("u1", 1, 10)]);

        var entries = await HandleAsync(TournamentStageGroup.GroupStage);

        entries.Should().HaveCount(2);
        entries.Single(e => e.UserId == "u2").TotalPoints.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldShowTheAbbreviatedNameOnScreen()
    {
        Given(members: [Member("u1", "Ada", "Lovelace")]);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().PlayerName.Should().Be("Ada L");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldReportWhetherARoundIsInProgressAnywhereInTheSeason(bool inProgress)
    {
        Given(hasRoundInProgress: inProgress);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Single().IsRoundInProgress.Should().Be(inProgress);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenTheLeagueHasNoApprovedMembers()
    {
        Given(members: []);

        (await HandleAsync(TournamentStageGroup.GroupStage)).Should().BeEmpty();
    }

    #endregion

    private void Given(
        IReadOnlyList<LeaderboardParticipantRow>? members = null,
        IReadOnlyList<SeasonRoundStageRow>? rounds = null,
        IReadOnlyList<MemberRoundPointsByRoundRow>? points = null,
        bool hasRoundInProgress = false) =>
        _leaderboardQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new StageLeaderboardData(
                members ?? [Member("u1", "Ada", "Lovelace")],
                rounds ?? [GroupRound(1, RoundStatus.Completed), GroupRound(2, RoundStatus.InProgress)],
                points ?? [],
                hasRoundInProgress));

    private static LeaderboardParticipantRow Member(string userId, string first, string last) =>
        new(userId, first, last);

    private static SeasonRoundStageRow GroupRound(int roundId, RoundStatus status = RoundStatus.Completed) =>
        new(roundId, "Group Stage", status);

    private static SeasonRoundStageRow KnockoutRound(int roundId, RoundStatus status = RoundStatus.Completed) =>
        new(roundId, "Quarter-finals", status);

    private static MemberRoundPointsByRoundRow Points(string userId, int roundId, int boostedPoints) =>
        new(userId, roundId, boostedPoints);

    private async Task<IReadOnlyList<Contracts.Leaderboards.LeaderboardEntryDto>> HandleAsync(
        TournamentStageGroup stage) =>
        (await _handler.Handle(new GetStageLeaderboardQuery(LeagueId, stage, ViewerId), CancellationToken.None)).ToList();
}
