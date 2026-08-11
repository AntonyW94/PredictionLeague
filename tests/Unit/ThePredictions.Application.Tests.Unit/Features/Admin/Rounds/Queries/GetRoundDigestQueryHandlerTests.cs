using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Queries;

/// <summary>
/// The round-results email, one per player who took part.
///
/// Most of these tests are about who gets one and what it says about each of their leagues, because that is what the
/// single flat statement behind this used to decide - in an EXISTS, a windowed CTE, a CASE and two joins.
/// </summary>
public class GetRoundDigestQueryHandlerTests
{
    private const int RoundId = 42;
    private const int SeasonId = 7;
    private const int LeagueId = 100;
    private const string UserId = "user-me";

    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IRoundDigestQuery _roundDigestQuery = Substitute.For<IRoundDigestQuery>();
    private readonly GetRoundDigestQueryHandler _handler;

    public GetRoundDigestQueryHandlerTests()
    {
        _handler = new GetRoundDigestQueryHandler(_roundDigestQuery);
    }

    #region Who gets an email

    [Fact]
    public async Task Handle_ShouldSendNothing_WhenThereIsNoSuchRound()
    {
        // Arrange - no rounds at all, which is what a bad round id yields.
        Given(new RoundDigestData([], [], [], []));

        // Act
        var digests = await HandleAsync();

        // Assert
        digests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSendNothing_WhenTheRoundAskedAboutIsNotAmongTheSeasonsRounds()
    {
        // Arrange - players, leagues and points all present, but no round with this id. Reporting on whichever round
        // came back first would email everybody about the wrong one.
        Given(Data(
            rounds: [Round(13, "Another Round")],
            players: [Player()],
            memberships: [Membership()],
            leagueScores: [Score(UserId, 30)]));

        // Act
        var digests = await HandleAsync();

        // Assert
        digests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSendNothing_ToAPlayerWhoDidNotPredict()
    {
        // Everybody in the round is scored, including the players who forgot. An email telling somebody they got
        // nothing right in a round they never entered is the one thing this must not send.
        Given(Data(
            players: [Player() with { PredictionCount = 0 }],
            memberships: [Membership()],
            leagueScores: [Score(UserId, 30)]));

        // Act
        var digests = await HandleAsync();

        // Assert
        digests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSendNothing_ToAPlayerWithNoLeaguesInTheSeason()
    {
        // Arrange
        Given(Data(players: [Player()], memberships: [], leagueScores: []));

        // Act
        var digests = await HandleAsync();

        // Assert - the email is a list of how their leagues went, so with no leagues there is nothing to send.
        digests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSendNothing_WhenTheirLeagueHasNotBeenScoredYet()
    {
        // The state a league sits in between a round finishing and its points being worked out. A row of zeroes would
        // read as a bad round rather than an unfinished calculation.
        Given(Data(players: [Player()], memberships: [Membership()], leagueScores: []));

        // Act
        var digests = await HandleAsync();

        // Assert
        digests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSendOneEmailPerPlayerWhoTookPart()
    {
        // Arrange
        Given(Data(
            players: [Player(), Player("user-other", "Grace")],
            memberships: [Membership(), Membership("user-other")],
            leagueScores: [Score(UserId, 30), Score("user-other", 40, "Grace", "Hopper")]));

        // Act
        var digests = await HandleAsync();

        // Assert
        digests.Select(digest => digest.UserId).Should().Equal(UserId, "user-other");
    }

    #endregion

    #region What the email says about the round

    [Fact]
    public async Task Handle_ShouldNameTheRound()
    {
        // Arrange
        Given(Data(rounds: [Round(12, "Quarter Finals")], players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.RoundName.Should().Be("Quarter Finals");
    }

    [Fact]
    public async Task Handle_ShouldNameARoundThatHasNoNameOfItsOwnByItsNumber()
    {
        // Every ordinary league round is like this. The statement this replaces put the blank straight into the email.
        Given(Data(rounds: [Round(12, displayName: null)], players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.RoundName.Should().Be("Round 12");
    }

    [Fact]
    public async Task Handle_ShouldReportTheirOwnScoringForTheRound()
    {
        // Arrange
        Given(Data(players: [Player() with { ExactScoreCount = 3, CorrectResultCount = 5 }], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.Email.Should().Be("ada@example.com");
        digest.FirstName.Should().Be("Ada");
        digest.ExactScoreCount.Should().Be(3);
        digest.CorrectResultCount.Should().Be(5);
    }

    #endregion

    #region What comes next

    [Fact]
    public async Task Handle_ShouldPointForwardToTheNextRoundOfTheSeason()
    {
        // Arrange
        Given(Data(
            rounds: [Round(12, null), Round(13, "Semi Finals", Deadline.AddDays(7)), Round(14, null, Deadline.AddDays(14))],
            players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert - the next one, not the one after it.
        digest.NextRoundName.Should().Be("Semi Finals");
        digest.NextRoundDeadlineUtc.Should().Be(Deadline.AddDays(7));
    }

    [Fact]
    public async Task Handle_ShouldChooseTheNextRoundByNumberRatherThanByDeadline()
    {
        // A round rescheduled into next month is still the one that comes next.
        Given(Data(
            rounds: [Round(12, null), Round(13, "Rescheduled", Deadline.AddMonths(2)), Round(14, "Later", Deadline.AddDays(14))],
            players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.NextRoundName.Should().Be("Rescheduled");
    }

    [Fact]
    public async Task Handle_ShouldNameTheNextRoundByItsNumber_WhenItHasNoNameOfItsOwn()
    {
        // Arrange
        Given(Data(
            rounds: [Round(12, null), Round(13, null, Deadline.AddDays(7))],
            players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.NextRoundName.Should().Be("Round 13");
    }

    [Fact]
    public async Task Handle_ShouldSayNothingAboutWhatComesNext_AfterTheFinalRound()
    {
        // Arrange - the only round in the season.
        Given(Data(players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert - the email leaves the line out rather than inventing a fixture.
        digest.NextRoundName.Should().BeNull();
        digest.NextRoundDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreEarlierRoundsWhenLookingForTheNextOne()
    {
        // Arrange
        Given(Data(
            rounds: [Round(11, "Earlier", Deadline.AddDays(-7)), Round(12, null)],
            players: [Player()], memberships: [Membership()], leagueScores: [Score(UserId, 30)]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.NextRoundName.Should().BeNull();
    }

    #endregion

    #region What the email says about each league

    [Fact]
    public async Task Handle_ShouldReportTheirPointsAndPositionInEachLeague()
    {
        // Arrange
        Given(Data(
            players: [Player()],
            memberships: [Membership() with { OverallRank = 3 }],
            leagueScores: [Score(UserId, 30)]));

        // Act
        var league = (await HandleAsync()).Single().Leagues.Single();

        // Assert
        league.LeagueId.Should().Be(LeagueId);
        league.LeagueName.Should().Be("Alpha League");
        league.Points.Should().Be(30);
        league.Position.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldReportHowManyPlacesTheyHaveMoved()
    {
        // Arrange - fifth before the round, third after it.
        Given(Data(
            players: [Player()],
            memberships: [Membership() with { OverallRank = 3, SnapshotOverallRank = 5 }],
            leagueScores: [Score(UserId, 30)]));

        // Act
        var league = (await HandleAsync()).Single().Leagues.Single();

        // Assert
        league.PositionDelta.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotClaimAnyMovement_WhenThereIsNoEarlierPosition()
    {
        // Arrange
        Given(Data(
            players: [Player()],
            memberships: [Membership() with { OverallRank = 3, SnapshotOverallRank = null }],
            leagueScores: [Score(UserId, 30)]));

        // Act
        var league = (await HandleAsync()).Single().Leagues.Single();

        // Assert - nothing rather than zero, because the email turns this into an arrow.
        league.PositionDelta.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNameTheLeaguesTopScorerForTheRound()
    {
        // Arrange
        Given(Data(
            players: [Player()],
            memberships: [Membership()],
            leagueScores: [Score(UserId, 30), Score("user-other", 45, "Grace", "Hopper")]));

        // Act
        var league = (await HandleAsync()).Single().Leagues.Single();

        // Assert - shown the way players are shown to each other everywhere else.
        league.TopScorerName.Should().Be("Grace H");
        league.TopScorerPoints.Should().Be(45);
    }

    [Fact]
    public async Task Handle_ShouldSettleAJointTopScoreAlphabeticallyByFullName()
    {
        // Two players on the same points who share a first name. The old tie-break was on first name alone, which
        // cannot separate them, and disagreed with the tie-break every leaderboard on the site uses.
        Given(Data(
            players: [Player()],
            memberships: [Membership()],
            leagueScores:
            [
                Score(UserId, 30),
                Score("user-a", 45, "Ada", "Zeta"),
                Score("user-b", 45, "Ada", "Lamarr")
            ]));

        // Act
        var league = (await HandleAsync()).Single().Leagues.Single();

        // Assert - Lamarr before Zeta. A first-name tie-break could not tell them apart at all, so whichever row
        // happened to arrive first would have won.
        league.TopScorerName.Should().Be("Ada L");
        league.TopScorerPoints.Should().Be(45);
    }

    [Fact]
    public async Task Handle_ShouldNameThemAsTheTopScorer_WhenTheyWonTheRound()
    {
        // Arrange
        Given(Data(
            players: [Player()],
            memberships: [Membership()],
            leagueScores: [Score(UserId, 50), Score("user-other", 45, "Grace", "Hopper")]));

        // Act
        var league = (await HandleAsync()).Single().Leagues.Single();

        // Assert
        league.TopScorerName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldListTheirLeaguesByName()
    {
        // Arrange
        // The ids deliberately run the other way to the names, so ordering by whichever came to hand would fail.
        Given(Data(
            players: [Player()],
            memberships:
            [
                Membership() with { LeagueId = 100, LeagueName = "Zulu League" },
                Membership() with { LeagueId = 200, LeagueName = "Bravo League" },
                Membership() with { LeagueId = 300, LeagueName = "Alpha League" }
            ],
            leagueScores:
            [
                Score(UserId, 30) with { LeagueId = 100 },
                Score(UserId, 20) with { LeagueId = 200 },
                Score(UserId, 10) with { LeagueId = 300 }
            ]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.Leagues.Select(league => league.LeagueName).Should().Equal("Alpha League", "Bravo League", "Zulu League");
        digest.Leagues.Select(league => league.LeagueId).Should().Equal(300, 200, 100);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutALeagueWhereTheyHaveNoScoreButOthersDo()
    {
        // A player who joined a league after the round was scored. Their own points do not exist, so the league has
        // nothing to say about their round.
        Given(Data(
            players: [Player()],
            memberships:
            [
                Membership(),
                Membership() with { LeagueId = 200, LeagueName = "Bravo League" }
            ],
            leagueScores:
            [
                Score(UserId, 30),
                Score("user-other", 40, "Grace", "Hopper") with { LeagueId = 200 }
            ]));

        // Act
        var digest = (await HandleAsync()).Single();

        // Assert
        digest.Leagues.Select(league => league.LeagueId).Should().Equal(LeagueId);
    }

    [Fact]
    public async Task Handle_ShouldNotGiveOnePlayersPointsToAnother()
    {
        // Arrange
        Given(Data(
            players: [Player(), Player("user-other", "Grace")],
            memberships: [Membership(), Membership("user-other")],
            leagueScores: [Score(UserId, 30), Score("user-other", 45, "Grace", "Hopper")]));

        // Act
        var digests = await HandleAsync();

        // Assert
        digests.Single(digest => digest.UserId == UserId).Leagues.Single().Points.Should().Be(30);
        digests.Single(digest => digest.UserId == "user-other").Leagues.Single().Points.Should().Be(45);
    }

    [Fact]
    public async Task Handle_ShouldAskForTheRoundRequested()
    {
        // Arrange
        Given(new RoundDigestData([], [], [], []));

        // Act
        await HandleAsync();

        // Assert
        await _roundDigestQuery.Received(1).ExecuteAsync(RoundId, Arg.Any<CancellationToken>());
    }

    #endregion

    private void Given(RoundDigestData data) =>
        _roundDigestQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(data);

    private static RoundDigestData Data(
        RoundDigestRoundRow[]? rounds = null,
        RoundDigestPlayerRow[]? players = null,
        RoundDigestMembershipRow[]? memberships = null,
        RoundLeagueScoreRow[]? leagueScores = null) =>
        new(rounds ?? [Round(12, null)], players ?? [], memberships ?? [], leagueScores ?? []);

    /// <summary>
    /// Round 12 is the one being reported on; any other number is another round of the same season. A blank name is how
    /// an unnamed round arrives, which the column allows even though nothing in the database is unnamed today.
    /// </summary>
    private static RoundDigestRoundRow Round(int roundNumber, string? displayName, DateTime? deadlineUtc = null) =>
        new(roundNumber == 12 ? RoundId : RoundId + roundNumber, roundNumber, displayName ?? string.Empty, deadlineUtc ?? Deadline);

    private static RoundDigestPlayerRow Player(string userId = UserId, string firstName = "Ada") =>
        new(userId, $"{firstName.ToLowerInvariant()}@example.com", firstName,
            ExactScoreCount: 1, CorrectResultCount: 2, PredictionCount: 10);

    private static RoundDigestMembershipRow Membership(string userId = UserId) =>
        new(userId, LeagueId, "Alpha League", OverallRank: 1, SnapshotOverallRank: 1);

    private static RoundLeagueScoreRow Score(string userId, int boostedPoints, string firstName = "Ada", string lastName = "Lovelace") =>
        new(LeagueId, userId, firstName, lastName, boostedPoints);

    private Task<IReadOnlyList<UserRoundDigest>> HandleAsync() =>
        _handler.Handle(new GetRoundDigestQuery(RoundId), CancellationToken.None);
}
