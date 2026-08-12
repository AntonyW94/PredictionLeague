using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The My Leagues tile - the largest read on the site, and the one with a performance history behind it.
///
/// Every rank shown here is still read from the <c>LeagueMemberStats</c> cache and none is recomputed, per
/// ADR-0015. What these tests cover is the dozen rules that were wrapped around those ranks: which round the tile
/// is about, what it is called, which cached column a tournament uses, what the pot is worth, and the two win
/// counts.
/// </summary>
public class GetMyLeaguesQueryHandlerTests
{
    private const string MeId = "user-me";
    private const string RivalId = "user-rival";
    private const int LeagueId = 42;
    private const int SeasonId = 7;

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime January = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime February = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly IMyLeaguesQuery _myLeaguesQuery = Substitute.For<IMyLeaguesQuery>();
    private readonly GetMyLeaguesQueryHandler _handler;

    public GetMyLeaguesQueryHandlerTests()
    {
        _handler = new GetMyLeaguesQueryHandler(_myLeaguesQuery, new TestDateTimeProvider(Now));
    }

    #region Nothing to show

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThePlayerIsInNoLeagues()
    {
        // Arrange
        Given();

        // Act
        var tiles = await HandleAsync();

        // Assert
        tiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnATile_ForALeagueWhoseSeasonHasNotStarted()
    {
        // Arrange - no rounds at all.
        Given(leagues: [League()]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - the old query's NULLs and ISNULL zeroes.
        tile.CurrentRound.Should().BeNull();
        tile.CurrentMonth.Should().BeNull();
        tile.RoundStatus.Should().BeNull();
        tile.RoundStartDateUtc.Should().BeNull();
        tile.InProgressCount.Should().Be(0);
        tile.CompletedCount.Should().Be(0);
        tile.StageName.Should().BeNull();
        tile.RoundsWon.Should().Be(0);
        tile.MonthsWon.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnATile_ForALeagueWithNoCachedRanksYet()
    {
        // Arrange - no LeagueMemberStats row.
        Given(leagues: [League()], rounds: [Round(1, RoundStatus.InProgress)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - a missing rank is not a zero; null is what suppresses the arrow on the tile.
        tile.Rank.Should().BeNull();
        tile.MonthRank.Should().BeNull();
        tile.RoundRank.Should().BeNull();
        tile.StageRank.Should().BeNull();
    }

    #endregion

    #region Which round the tile is about

    [Fact]
    public async Task Handle_ShouldShowTheRoundInPlay()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds:
            [
                Round(1, RoundStatus.Completed, completedDateUtc: Now.AddHours(-1)),
                Round(2, RoundStatus.InProgress),
                Round(3, RoundStatus.Published)
            ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.CurrentRound.Should().Be("Round 2");
        tile.RoundStatus.Should().Be(nameof(RoundStatus.InProgress));
    }

    [Fact]
    public async Task Handle_ShouldStillShowARoundThatFinishedYesterday()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds:
            [
                Round(1, RoundStatus.Completed, completedDateUtc: Now.AddHours(-12)),
                Round(2, RoundStatus.Published)
            ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.CurrentRound.Should().Be("Round 1");
    }

    [Fact]
    public async Task Handle_ShouldMoveOnToTheNextRound_OnceTheGracePeriodHasPassed()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds:
            [
                Round(1, RoundStatus.Completed, completedDateUtc: Now.AddHours(-49)),
                Round(2, RoundStatus.Published)
            ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.CurrentRound.Should().Be("Round 2");
    }

    [Fact]
    public async Task Handle_ShouldNeverShowADraftRound()
    {
        // Arrange
        Given(leagues: [League()], rounds: [Round(1, RoundStatus.Draft)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.CurrentRound.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheActiveRoundsMatchCounts()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress, inProgressMatches: 2, completedMatches: 5)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - these drive whether the tile shows a live arrow.
        tile.InProgressCount.Should().Be(2);
        tile.CompletedCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldLabelTheRoundByItsNumber_EvenWhenItHasAName()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress, displayName: "Semi Finals")]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - preserved from the old SQL, which ignored DisplayName. Flagged in the plan as a question.
        tile.CurrentRound.Should().Be("Round 1");
    }

    #endregion

    #region The second slot: month, or exact scores

    [Fact]
    public async Task Handle_ShouldNameTheRoundsMonth_ForALeague()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress, startDateUtc: February)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - in English, whatever language the database speaks.
        tile.CurrentMonth.Should().Be("February");
    }

    [Fact]
    public async Task Handle_ShouldLabelTheSlotExactScores_ForATournament()
    {
        // Arrange
        Given(
            leagues: [League(competitionType: CompetitionType.Tournament)],
            rounds: [Round(1, RoundStatus.InProgress, startDateUtc: February)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.CurrentMonth.Should().Be("Exact Scores");
    }

    [Fact]
    public async Task Handle_ShouldUseTheMonthRank_ForALeague()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress)],
            stats: [Stats(monthRank: 3, exactScoresRank: 99, snapshotMonthRank: 4, preRoundExactScoresRank: 98)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.MonthRank.Should().Be(3);
        tile.PreRoundMonthRank.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldUseTheExactScoresRank_ForATournament()
    {
        // Arrange
        Given(
            leagues: [League(competitionType: CompetitionType.Tournament)],
            rounds: [Round(1, RoundStatus.InProgress)],
            stats: [Stats(monthRank: 3, exactScoresRank: 99, snapshotMonthRank: 4, preRoundExactScoresRank: 98)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - a tournament promises exact scores in that slot, so it can never show a points rank there.
        tile.MonthRank.Should().Be(99);
        tile.PreRoundMonthRank.Should().Be(98);
    }

    #endregion

    #region Round ranks before a round starts

    [Fact]
    public async Task Handle_ShouldRankEveryoneFirst_BeforeTheRoundStarts()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.Published)],
            stats: [Stats(liveRoundRank: 7, stableRoundRank: 8)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - nobody has scored in it yet, so nobody is behind.
        tile.RoundRank.Should().Be(1);
        tile.StableRoundRank.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUseTheCachedRoundRanks_OnceTheRoundIsUnderWay()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress)],
            stats: [Stats(liveRoundRank: 7, stableRoundRank: 8)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.RoundRank.Should().Be(7);
        tile.StableRoundRank.Should().Be(8);
    }

    [Fact]
    public async Task Handle_ShouldPassTheCachedRanksStraightThrough()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress)],
            stats: [Stats(overallRank: 2, snapshotOverallRank: 5, stageRank: 6, preRoundStageRank: 9)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert - read, never recomputed. ADR-0015.
        tile.Rank.Should().Be(2);
        tile.PreRoundOverallRank.Should().Be(5);
        tile.StageRank.Should().Be(6);
        tile.PreRoundStageRank.Should().Be(9);
    }

    [Fact]
    public async Task Handle_ShouldShowNoPositions_ForAPlayerWithNoCachedRanksYet()
    {
        // Arrange - the gap between joining a league and the next time the ranks are written.
        Given(leagues: [League()], rounds: [Round(1, RoundStatus.InProgress)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.Rank.Should().BeNull();
        tile.MonthRank.Should().BeNull();
        tile.RoundRank.Should().BeNull();
        tile.PreRoundOverallRank.Should().BeNull();
        tile.PreRoundMonthRank.Should().BeNull();
        tile.StableRoundRank.Should().BeNull();
        tile.StageRank.Should().BeNull();
        tile.PreRoundStageRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldStillRankAnUncachedPlayerFirst_BeforeTheRoundStarts()
    {
        // Everyone is joint first in a round nobody has scored in - including somebody with no cached row at all, who
        // would otherwise be shown as unranked in a round where nobody can be behind.
        Given(leagues: [League()], rounds: [Round(1, RoundStatus.Published)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.RoundRank.Should().Be(1);
        tile.StableRoundRank.Should().Be(1);
        tile.Rank.Should().BeNull();
    }

    #endregion

    #region The stage

    [Fact]
    public async Task Handle_ShouldNameTheGroupStage()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress, stages: "GroupStage|Group B")]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.StageName.Should().Be("Group Stage");
    }

    [Fact]
    public async Task Handle_ShouldNameTheKnockoutStage_ForAMappedRoundThatIsNotAGroupRound()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress, stages: "SemiFinals|Final")]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.StageName.Should().Be("Knockout Stage");
    }

    [Fact]
    public async Task Handle_ShouldShowNoStage_ForARoundWithNoMapping()
    {
        // Arrange
        Given(leagues: [League()], rounds: [Round(1, RoundStatus.InProgress)]);

        // Assert - not the same as a mapped round that is not a group round, which is a knockout.
        (await HandleAsync()).Single().StageName.Should().BeNull();
    }

    #endregion

    #region The prize pot

    [Fact]
    public async Task Handle_ShouldWorkOutThePotFromTheEntryFeesAndTheTopUp()
    {
        // Arrange
        Given(leagues:
        [
            League(price: 10m, memberCount: 12, prizeFundOverride: 50m, totalPaidOut: 70m, userWinnings: 30m)
        ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.TotalPrizeFund.Should().Be(170m);
        tile.PrizeMoneyRemaining.Should().Be(100m);
        tile.PrizeMoneyWon.Should().Be(30m);
        tile.EntryFee.Should().Be(10m);
        tile.MemberCount.Should().Be(12);
    }

    [Fact]
    public async Task Handle_ShouldReportNoPot_ForAFreeLeagueThatIsNotFunded()
    {
        // Arrange
        Given(leagues: [League(price: 0m, isFree: true, memberCount: 20)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.TotalPrizeFund.Should().Be(0m);
        tile.IsFree.Should().BeTrue();
    }

    #endregion

    #region Whether the league is over

    [Fact]
    public async Task Handle_ShouldReportTheLeagueAsFinished_WhenEveryRoundHasCompleted()
    {
        // Arrange
        Given(leagues: [League(numberOfRounds: 3, completedRoundCount: 3)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportTheLeagueAsUnfinished_WhileRoundsRemain()
    {
        // Arrange
        Given(leagues: [League(numberOfRounds: 3, completedRoundCount: 2)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheArchiveFlag()
    {
        // Arrange
        Given(leagues: [League(isArchivedByUser: true)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.IsArchivedByUser.Should().BeTrue();
    }

    #endregion

    #region Rounds and months won

    [Fact]
    public async Task Handle_ShouldCountTheRoundsThePlayerWon()
    {
        // Arrange - I win round 1, my rival wins round 2.
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.Completed), Round(2, RoundStatus.Completed)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 2, 5), Score(RivalId, 2, 30)
            ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.RoundsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountASharedRoundWin()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.Completed)],
            scores: [Score(MeId, 1, 20), Score(RivalId, 1, 20)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.RoundsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotCountARoundThatIsNotComplete()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.InProgress)],
            scores: [Score(MeId, 1, 20), Score(RivalId, 1, 10)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.RoundsWon.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotCountARoundNobodyScoredIn()
    {
        // Arrange
        Given(
            leagues: [League()],
            rounds: [Round(1, RoundStatus.Completed)],
            scores: [Score(MeId, 1, 0), Score(RivalId, 1, 0)]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.RoundsWon.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCountTheMonthsThePlayerWonOnTheMonthsTotal()
    {
        // Arrange - my rival takes two of January's three rounds, one big week gives me the month.
        Given(
            leagues: [League()],
            rounds:
            [
                Round(1, RoundStatus.Completed, startDateUtc: January),
                Round(2, RoundStatus.Completed, startDateUtc: January),
                Round(3, RoundStatus.Completed, startDateUtc: January)
            ],
            scores:
            [
                Score(MeId, 1, 1), Score(RivalId, 1, 10),
                Score(MeId, 2, 1), Score(RivalId, 2, 10),
                Score(MeId, 3, 30), Score(RivalId, 3, 1)
            ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.MonthsWon.Should().Be(1);
        tile.RoundsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSeparateMonthsByTheRoundsStartDate()
    {
        // Arrange - I take January, my rival takes February.
        Given(
            leagues: [League()],
            rounds:
            [
                Round(1, RoundStatus.Completed, startDateUtc: January),
                Round(2, RoundStatus.Completed, startDateUtc: February)
            ],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 2, 5), Score(RivalId, 2, 30)
            ]);

        // Act
        var tile = (await HandleAsync()).Single();

        // Assert
        tile.MonthsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountWinsPerLeague()
    {
        // Arrange - the same rounds, opposite outcomes in two leagues.
        Given(
            leagues: [League(), League(leagueId: 99, name: "Second League")],
            rounds: [Round(1, RoundStatus.Completed)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 1, 5, leagueId: 99), Score(RivalId, 1, 30, leagueId: 99)
            ]);

        // Act
        var tiles = (await HandleAsync()).ToList();

        // Assert - a win in one league is not a win in another.
        tiles.Single(tile => tile.Id == LeagueId).RoundsWon.Should().Be(1);
        tiles.Single(tile => tile.Id == 99).RoundsWon.Should().Be(0);
    }

    #endregion

    #region The order the tiles appear in

    [Fact]
    public async Task Handle_ShouldPutALeagueWithARoundInPlayFirst()
    {
        // Arrange - the live league started its season later, so only the in-play rule can lift it.
        Given(
            leagues:
            [
                League(seasonStartDateUtc: January, seasonId: 1),
                League(leagueId: 99, name: "Live League", seasonStartDateUtc: February, seasonId: 2)
            ],
            rounds: [Round(1, RoundStatus.Published, seasonId: 1), Round(1, RoundStatus.InProgress, seasonId: 2)]);

        // Act
        var tiles = (await HandleAsync()).ToList();

        // Assert
        tiles.Select(tile => tile.Id).Should().Equal(99, LeagueId);
    }

    [Fact]
    public async Task Handle_ShouldThenOrderByWhenTheSeasonStarted()
    {
        // Arrange
        Given(leagues:
        [
            League(seasonStartDateUtc: February),
            League(leagueId: 99, name: "Older", seasonStartDateUtc: January)
        ]);

        // Act
        var tiles = (await HandleAsync()).ToList();

        // Assert
        tiles.Select(tile => tile.Id).Should().Equal(99, LeagueId);
    }

    [Fact]
    public async Task Handle_ShouldThenPutTheHigherStakeLeagueFirst()
    {
        // Arrange
        Given(leagues: [League(price: 5m), League(leagueId: 99, name: "Big Money", price: 20m)]);

        // Act
        var tiles = (await HandleAsync()).ToList();

        // Assert
        tiles.Select(tile => tile.Id).Should().Equal(99, LeagueId);
    }

    [Fact]
    public async Task Handle_ShouldThenOrderByName()
    {
        // Arrange
        Given(leagues: [League(name: "Zebras"), League(leagueId: 99, name: "Aardvarks")]);

        // Act
        var tiles = (await HandleAsync()).ToList();

        // Assert
        tiles.Select(tile => tile.Name).Should().Equal("Aardvarks", "Zebras");
    }

    #endregion

    private void Given(
        IReadOnlyList<MyLeagueRow>? leagues = null,
        IReadOnlyList<MyLeagueRoundRow>? rounds = null,
        IReadOnlyList<MyLeagueRoundScoreRow>? scores = null,
        IReadOnlyList<MyLeagueStatsRow>? stats = null)
    {
        _myLeaguesQuery
            .ExecuteAsync(MeId, Arg.Any<CancellationToken>())
            .Returns(new MyLeaguesData(leagues ?? [], rounds ?? [], scores ?? [], stats ?? []));
    }

    private async Task<IEnumerable<MyLeagueDto>> HandleAsync() =>
        await _handler.Handle(new GetMyLeaguesQuery(MeId), CancellationToken.None);

    private static MyLeagueRow League(
        int leagueId = LeagueId,
        string? name = null,
        decimal price = 10m,
        decimal? prizeFundOverride = null,
        bool isFree = false,
        bool isArchivedByUser = false,
        int seasonId = SeasonId,
        CompetitionType competitionType = CompetitionType.League,
        DateTime? seasonStartDateUtc = null,
        int numberOfRounds = 38,
        int memberCount = 10,
        int completedRoundCount = 0,
        decimal totalPaidOut = 0m,
        decimal userWinnings = 0m) =>
        new(
            leagueId,
            name ?? $"League {leagueId}",
            price,
            prizeFundOverride,
            isFree,
            isArchivedByUser,
            seasonId,
            "2026/27",
            competitionType,
            seasonStartDateUtc ?? January,
            null,
            numberOfRounds,
            memberCount,
            completedRoundCount,
            totalPaidOut,
            userWinnings);

    private static MyLeagueRoundRow Round(
        int roundNumber,
        RoundStatus status,
        DateTime? startDateUtc = null,
        DateTime? completedDateUtc = null,
        int inProgressMatches = 0,
        int completedMatches = 0,
        string? stages = null,
        string? displayName = null,
        int seasonId = SeasonId) =>
        new(
            roundNumber,
            seasonId,
            roundNumber,
            displayName ?? string.Empty,
            startDateUtc ?? January,
            completedDateUtc,
            status,
            inProgressMatches,
            completedMatches,
            stages);

    private static MyLeagueRoundScoreRow Score(string userId, int roundId, int points, int leagueId = LeagueId) =>
        new(leagueId, userId, roundId, points);

    private static MyLeagueStatsRow Stats(
        int leagueId = LeagueId,
        int? overallRank = null,
        int? monthRank = null,
        int? liveRoundRank = null,
        int? snapshotOverallRank = null,
        int? snapshotMonthRank = null,
        int? stableRoundRank = null,
        int? stageRank = null,
        int? preRoundStageRank = null,
        int? exactScoresRank = null,
        int? preRoundExactScoresRank = null) =>
        new(
            leagueId,
            overallRank,
            monthRank,
            liveRoundRank,
            snapshotOverallRank,
            snapshotMonthRank,
            stableRoundRank,
            stageRank,
            preRoundStageRank,
            exactScoresRank,
            preRoundExactScoresRank);
}
