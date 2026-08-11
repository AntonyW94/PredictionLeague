using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The league dashboard's round grid.
///
/// The rules here are about secrecy as much as arithmetic: whether an opponent's prediction may be shown, and
/// whether a member who predicted nothing still gets a row. Both were invisible to tests while they were a
/// <c>CASE</c> over <c>GETUTCDATE()</c> and a <c>CROSS JOIN</c>.
/// </summary>
public class GetLeagueDashboardRoundResultsQueryHandlerTests
{
    private const int LeagueId = 42;
    private const int RoundId = 7;
    private const string ViewerId = "user-me";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureDeadline = Now.AddHours(1);
    private static readonly DateTime PastDeadline = Now.AddHours(-1);

    private readonly ILeagueRoundResultsQuery _resultsQuery = Substitute.For<ILeagueRoundResultsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeagueDashboardRoundResultsQueryHandler _handler;

    public GetLeagueDashboardRoundResultsQueryHandlerTests()
    {
        _handler = new GetLeagueDashboardRoundResultsQueryHandler(
            _resultsQuery, _membershipService, new TestDateTimeProvider(Now));
    }

    #region Membership

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _resultsQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default, CancellationToken.None);
    }

    #endregion

    #region Prediction secrecy

    [Fact]
    public async Task Handle_ShouldHideAnotherMembersPrediction_WhenTheFixtureHasNotLocked()
    {
        // Arrange
        Given(
            deadlineUtc: FutureDeadline,
            fixtures: [Fixture(1)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: [Prediction("rival", 1, 3, 0)]);

        // Act
        var cell = (await HandleAsync()).Single().Predictions.Single();

        // Assert
        cell.IsHidden.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldShowTheViewersOwnPrediction_WhenTheFixtureHasNotLocked()
    {
        // Arrange
        Given(
            deadlineUtc: FutureDeadline,
            fixtures: [Fixture(1)],
            members: [Member(ViewerId, "Ada", "Lovelace")],
            predictions: [Prediction(ViewerId, 1, 3, 0)]);

        // Act
        var cell = (await HandleAsync()).Single().Predictions.Single();

        // Assert
        cell.IsHidden.Should().BeFalse("a player always sees their own prediction.");
        cell.HomeScore.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldShowAnotherMembersPrediction_OnceTheFixtureHasLocked()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: [Prediction("rival", 1, 3, 0)]);

        // Act
        var cell = (await HandleAsync()).Single().Predictions.Single();

        // Assert
        cell.IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldHideOnlyTheFixturesStillOpen_WhenOneHasACustomLockTime()
    {
        // Arrange - the round is open for another hour, but fixture 1 kicked off early.
        Given(
            deadlineUtc: FutureDeadline,
            fixtures: [Fixture(1, customLockTimeUtc: Now.AddMinutes(-30)), Fixture(2)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: [Prediction("rival", 1, 3, 0), Prediction("rival", 2, 1, 1)]);

        // Act
        var cells = (await HandleAsync()).Single().Predictions;

        // Assert - the grid can be half revealed, which the round's deadline alone could never express.
        cells.Single(cell => cell.MatchId == 1).IsHidden.Should().BeFalse();
        cells.Single(cell => cell.MatchId == 2).IsHidden.Should().BeTrue();
    }

    #endregion

    #region The grid is dense

    [Fact]
    public async Task Handle_ShouldGiveEveryMemberACellForEveryFixture_EvenWithNoPrediction()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1), Fixture(2), Fixture(3)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: [Prediction("rival", 2, 3, 0)]);

        // Act
        var cells = (await HandleAsync()).Single().Predictions;

        // Assert - the columns have to line up across the rows.
        cells.Select(cell => cell.MatchId).Should().Equal(1, 2, 3);
        cells.Single(cell => cell.MatchId == 1).HomeScore.Should().BeNull();
        cells.Single(cell => cell.MatchId == 1).Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public async Task Handle_ShouldStillReturnAMember_WhenTheyPredictedNothingAtAll()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: []);

        // Act
        var row = (await HandleAsync()).Single();

        // Assert
        row.HasPredicted.Should().BeFalse();
        row.Predictions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldOrderTheCellsByKickOff()
    {
        // Arrange - fixture 1 kicks off last.
        Given(
            deadlineUtc: PastDeadline,
            fixtures:
            [
                Fixture(1, kickOffUtc: Now.AddDays(2)),
                Fixture(2, kickOffUtc: Now.AddDays(1))
            ],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: []);

        // Act
        var cells = (await HandleAsync()).Single().Predictions;

        // Assert
        cells.Select(cell => cell.MatchId).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveAPostponedFixtureOffTheGrid()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1), Fixture(2, status: MatchStatus.Postponed)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: []);

        // Act
        var cells = (await HandleAsync()).Single().Predictions;

        // Assert
        cells.Select(cell => cell.MatchId).Should().Equal(1);
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled)]
    [InlineData(MatchStatus.InProgress)]
    [InlineData(MatchStatus.Completed)]
    public async Task Handle_ShouldKeepAFixtureOnTheGrid_WhateverStageOfPlayItIsAt(MatchStatus status)
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1, status: status)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: []);

        // Act
        var cells = (await HandleAsync()).Single().Predictions;

        // Assert
        cells.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldReportHasPredicted_WhenAtLeastOneScoreIsEntered()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1), Fixture(2)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: [Prediction("rival", 2, 0, 0)]);

        // Act
        var row = (await HandleAsync()).Single();

        // Assert
        row.HasPredicted.Should().BeTrue("nil-nil is a prediction.");
    }

    [Fact]
    public async Task Handle_ShouldCarryEachPredictionsOutcome()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("rival", "Grace", "Hopper")],
            predictions: [Prediction("rival", 1, 3, 0, PredictionOutcome.ExactScore)]);

        // Act
        var cell = (await HandleAsync()).Single().Predictions.Single();

        // Assert
        cell.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    #endregion

    #region Ranking

    [Fact]
    public async Task Handle_ShouldRankByPointsForTheRound()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u1", "Ada", "Lovelace"), Member("u2", "Grace", "Hopper")],
            predictions: [],
            points: [new MemberRoundPointsRow("u1", 4), new MemberRoundPointsRow("u2", 9)]);

        // Act
        var rows = (await HandleAsync()).ToList();

        // Assert
        rows.Select(row => row.UserId).Should().Equal("u2", "u1");
        rows.Select(row => row.Rank).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Handle_ShouldGiveJointPositionsTheSameRankAndSkipTheNext()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members:
            [
                Member("u1", "Ada", "Lovelace"),
                Member("u2", "Grace", "Hopper"),
                Member("u3", "Alan", "Turing")
            ],
            predictions: [],
            points: [new MemberRoundPointsRow("u1", 9), new MemberRoundPointsRow("u2", 9)]);

        // Act
        var rows = (await HandleAsync()).ToList();

        // Assert - 1, 1, 3, as RANK() gave.
        rows.Select(row => row.Rank).Should().Equal(1, 1, 3);
    }

    [Fact]
    public async Task Handle_ShouldOrderJointPositionsByFullName()
    {
        // Arrange - "Ada Lovelace" before "Grace Hopper", which the old ORDER BY on the abbreviated
        // "Ada L" / "Grace H" happened to agree with.
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u2", "Grace", "Hopper"), Member("u1", "Ada", "Lovelace")],
            predictions: [],
            points: [new MemberRoundPointsRow("u1", 9), new MemberRoundPointsRow("u2", 9)]);

        // Act
        var rows = (await HandleAsync()).ToList();

        // Assert
        rows.Select(row => row.PlayerName).Should().Equal("Ada L", "Grace H");
    }

    [Fact]
    public async Task Handle_ShouldScoreAMemberWithNoResultRowAsZero()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u1", "Ada", "Lovelace")],
            predictions: [],
            points: []);

        // Act
        var row = (await HandleAsync()).Single();

        // Assert - the old COALESCE, which kept them on the grid rather than dropping the row.
        row.TotalPoints.Should().Be(0);
        row.Rank.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldAbbreviateThePlayerName()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u1", "Ada", "Lovelace")],
            predictions: []);

        // Act
        var row = (await HandleAsync()).Single();

        // Assert
        row.PlayerName.Should().Be("Ada L");
    }

    #endregion

    #region Boosts

    [Fact]
    public async Task Handle_ShouldReportTheBoostAMemberPlayed()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u1", "Ada", "Lovelace")],
            predictions: [],
            boostUsages: [new MemberBoostUsageRow("u1", "DOUBLE", "double.png")]);

        // Act
        var row = (await HandleAsync()).Single();

        // Assert
        row.AppliedBoostCode.Should().Be("DOUBLE");
        row.AppliedBoostImageUrl.Should().Be("double.png");
    }

    [Fact]
    public async Task Handle_ShouldReportNoBoost_WhenTheMemberPlayedNone()
    {
        // Arrange
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u1", "Ada", "Lovelace"), Member("u2", "Grace", "Hopper")],
            predictions: [],
            boostUsages: [new MemberBoostUsageRow("u2", "DOUBLE", "double.png")]);

        // Act
        var row = (await HandleAsync()).Single(entry => entry.UserId == "u1");

        // Assert
        row.AppliedBoostCode.Should().BeNull();
        row.AppliedBoostImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldTakeTheFirstBoostByCode_WhenAMemberSomehowPlayedTwo()
    {
        // Arrange - nothing in the schema forbids it, and the old query took whichever row the join produced first.
        Given(
            deadlineUtc: PastDeadline,
            fixtures: [Fixture(1)],
            members: [Member("u1", "Ada", "Lovelace")],
            predictions: [],
            boostUsages:
            [
                new MemberBoostUsageRow("u1", "TRIPLE", "triple.png"),
                new MemberBoostUsageRow("u1", "DOUBLE", "double.png")
            ]);

        // Act
        var row = (await HandleAsync()).Single();

        // Assert
        row.AppliedBoostCode.Should().Be("DOUBLE");
    }

    #endregion

    #region A round that is not there

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheRoundDoesNotExist()
    {
        // Arrange
        _resultsQuery
            .ExecuteAsync(LeagueId, RoundId, Arg.Any<CancellationToken>())
            .Returns((LeagueRoundResultsData?)null);

        // Act
        var rows = await HandleAsync();

        // Assert - what the old statement's join produced, now stated.
        rows.Should().BeEmpty();
    }

    #endregion

    private void Given(
        DateTime deadlineUtc,
        IReadOnlyList<Match> fixtures,
        IReadOnlyList<LeaderboardParticipantRow> members,
        IReadOnlyList<MemberPredictionRow> predictions,
        IReadOnlyList<MemberRoundPointsRow>? points = null,
        IReadOnlyList<MemberBoostUsageRow>? boostUsages = null)
    {
        var round = new Round(
            id: RoundId,
            seasonId: 1,
            roundNumber: 3,
            displayName: "Round 3",
            startDateUtc: deadlineUtc.AddDays(-1),
            deadlineUtc: deadlineUtc,
            status: RoundStatus.InProgress,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: fixtures.ToList(),
            resultsDigestSentUtc: null);

        _resultsQuery
            .ExecuteAsync(LeagueId, RoundId, Arg.Any<CancellationToken>())
            .Returns(new LeagueRoundResultsData(round, members, predictions, points ?? [], boostUsages ?? []));
    }

    private async Task<IEnumerable<PredictionResultDto>> HandleAsync() =>
        await _handler.Handle(
            new GetLeagueDashboardRoundResultsQuery(LeagueId, RoundId, ViewerId),
            CancellationToken.None);

    private static LeaderboardParticipantRow Member(string userId, string firstName, string lastName) =>
        new(userId, firstName, lastName);

    private static MemberPredictionRow Prediction(
        string userId,
        int matchId,
        int homeScore,
        int awayScore,
        PredictionOutcome outcome = PredictionOutcome.Pending) =>
        new(userId, matchId, homeScore, awayScore, outcome);

    private static Match Fixture(
        int id,
        DateTime? kickOffUtc = null,
        DateTime? customLockTimeUtc = null,
        MatchStatus status = MatchStatus.Scheduled) =>
        new(
            id: id,
            roundId: RoundId,
            homeTeamId: 10,
            awayTeamId: 20,
            matchDateTimeUtc: kickOffUtc ?? Now.AddDays(id),
            customLockTimeUtc: customLockTimeUtc,
            status: status,
            actualHomeTeamScore: null,
            actualAwayTeamScore: null,
            externalId: null,
            matchNumber: id,
            placeholderHomeName: null,
            placeholderAwayName: null,
            apiRoundName: null);
}
