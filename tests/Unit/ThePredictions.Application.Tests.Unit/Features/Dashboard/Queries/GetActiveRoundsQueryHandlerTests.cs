using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The rounds on a player's dashboard.
///
/// Most of these tests are about the prediction split - the home/draw/away breakdown of everybody's predictions - which must
/// not be visible while a match can still be predicted, or players could simply copy the crowd. In a combined round that
/// decision is per match rather than per round.
/// </summary>
public class GetActiveRoundsQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IActiveRoundsQuery _activeRoundsQuery = Substitute.For<IActiveRoundsQuery>();
    private readonly GetActiveRoundsQueryHandler _handler;

    public GetActiveRoundsQueryHandlerTests()
    {
        _handler = new GetActiveRoundsQueryHandler(_activeRoundsQuery, new TestDateTimeProvider(Now));
    }

    #region Which rounds appear

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThereAreNoActiveRounds()
    {
        // Arrange
        Given();

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIncludeARoundStillOpenForPredictions()
    {
        // Arrange
        Given(rounds: [Round(1, deadlineUtc: Now.AddHours(1))], matches: [Match(1)]);

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldIncludeAClosedRound_WhenThePlayerPredictedInIt()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(-1), hasUserPredicted: true)],
            matches: [Match(1)]);

        // Act
        var rounds = await HandleAsync();

        // Assert - a player who predicted wants to see how it went.
        rounds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldDropAClosedRound_WhenThePlayerDidNotPredictInIt()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(-1), hasUserPredicted: false)],
            matches: [Match(1)]);

        // Act
        var rounds = await HandleAsync();

        // Assert - there is nothing they can do about it and nothing of theirs to look at.
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldDropARoundWithNoConfirmedTeams()
    {
        // Arrange - a round of placeholders, which a tournament has before its group stage resolves.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1), hasConfirmedMatch: false)],
            matches: [Match(1, areTeamsConfirmed: false)]);

        // Act
        var rounds = await HandleAsync();

        // Assert - nothing a player can act on yet.
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldKeepARoundOpen_WhenALaterMatchLocksAfterTheRoundDeadline()
    {
        // Arrange - a combined round: the round deadline has passed, but one match locks tomorrow.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(-1), hasUserPredicted: false)],
            matches:
            [
                Match(1),
                Match(1, customLockTimeUtc: Now.AddDays(1))
            ]);

        // Act
        var rounds = await HandleAsync();

        // Assert - still predictable, so still on the dashboard even though the player has predicted nothing.
        rounds.Should().HaveCount(1);
        rounds.Single().LatestPredictionDeadlineUtc.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoundDeadlineAsTheLatest_WhenNoMatchLocksLater()
    {
        // Arrange
        var deadline = Now.AddHours(1);
        Given(
            rounds: [Round(1, deadlineUtc: deadline)],
            matches: [Match(1), Match(1, customLockTimeUtc: deadline.AddHours(-2))]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert - an earlier custom lock does not shorten the round; it only locks that one match sooner.
        round.LatestPredictionDeadlineUtc.Should().Be(deadline);
    }

    [Fact]
    public async Task Handle_ShouldPutARoundInProgressFirst()
    {
        // Arrange - the in-progress round has the later deadline, so only the status rule can lift it.
        Given(
            rounds:
            [
                Round(1, deadlineUtc: Now.AddHours(1), status: RoundStatus.Published),
                Round(2, deadlineUtc: Now.AddHours(2), status: RoundStatus.InProgress)
            ],
            matches: [Match(1), Match(2)]);

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Select(round => round.Id).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldThenOrderByDeadline()
    {
        // Arrange
        Given(
            rounds:
            [
                Round(1, deadlineUtc: Now.AddHours(2)),
                Round(2, deadlineUtc: Now.AddHours(1))
            ],
            matches: [Match(1), Match(2)]);

        // Act
        var rounds = await HandleAsync();

        // Assert - soonest first, because that is what a player needs to act on.
        rounds.Select(round => round.Id).Should().Equal(2, 1);
    }

    #endregion

    #region The prediction split

    [Fact]
    public async Task Handle_ShouldHideThePredictionSplit_WhileTheMatchIsStillOpen()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1))],
            matches: [Match(1, homeCount: 5, drawCount: 3, awayCount: 2)]);

        // Act
        var match = (await HandleAsync()).Single().Matches.Single();

        // Assert - zeroed rather than merely flagged, so the numbers never reach a browser that could read them anyway.
        match.IsPredictionRevealed.Should().BeFalse();
        match.HomeCount.Should().Be(0);
        match.DrawCount.Should().Be(0);
        match.AwayCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRevealThePredictionSplit_OnceTheRoundDeadlineHasPassed()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(-1), hasUserPredicted: true)],
            matches: [Match(1, homeCount: 5, drawCount: 3, awayCount: 2)]);

        // Act
        var match = (await HandleAsync()).Single().Matches.Single();

        // Assert
        match.IsPredictionRevealed.Should().BeTrue();
        match.HomeCount.Should().Be(5);
        match.DrawCount.Should().Be(3);
        match.AwayCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldRevealTheSplit_AtTheDeadlineItself()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now, hasUserPredicted: true)],
            matches: [Match(1, homeCount: 5)]);

        // Act
        var match = (await HandleAsync()).Single().Matches.Single();

        // Assert - the boundary is inclusive, the same way a prediction is locked at its deadline rather than a tick after.
        match.IsPredictionRevealed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldKeepTheSplitHidden_WhenTheMatchLocksAfterThePassedRoundDeadline()
    {
        // Arrange - the round deadline has gone, but this match has its own later lock.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(-1), hasUserPredicted: true)],
            matches: [Match(1, customLockTimeUtc: Now.AddHours(1), homeCount: 5)]);

        // Act
        var match = (await HandleAsync()).Single().Matches.Single();

        // Assert
        match.IsPredictionRevealed.Should().BeFalse();
        match.HomeCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRevealTheSplit_WhenTheMatchLockedBeforeTheStillFutureRoundDeadline()
    {
        // Arrange - an early kick-off inside a round that is otherwise still open.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(2))],
            matches: [Match(1, customLockTimeUtc: Now.AddHours(-1), homeCount: 5)]);

        // Act
        var match = (await HandleAsync()).Single().Matches.Single();

        // Assert
        match.IsPredictionRevealed.Should().BeTrue();
        match.HomeCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldDecideTheSplitPerMatch_WithinTheSameRound()
    {
        // Arrange - one match locked, one still open, in a single round.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(2))],
            matches:
            [
                Match(1, customLockTimeUtc: Now.AddHours(-1), homeCount: 5, kickOffUtc: Now.AddHours(-1)),
                Match(1, customLockTimeUtc: Now.AddHours(3), homeCount: 7, kickOffUtc: Now.AddHours(3))
            ]);

        // Act
        var matches = (await HandleAsync()).Single().Matches.ToList();

        // Assert - this is why the rule asks about the match and not the round.
        matches[0].IsPredictionRevealed.Should().BeTrue();
        matches[0].HomeCount.Should().Be(5);
        matches[1].IsPredictionRevealed.Should().BeFalse();
        matches[1].HomeCount.Should().Be(0);
    }

    #endregion

    #region The outcome summary

    [Fact]
    public async Task Handle_ShouldSummariseOutcomes_OnceTheDeadlineHasPassedForAPlayerWhoPredicted()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(-1), hasUserPredicted: true)],
            matches:
            [
                Match(1, outcome: PredictionOutcome.ExactScore),
                Match(1, outcome: PredictionOutcome.CorrectResult),
                Match(1, outcome: PredictionOutcome.Incorrect),
                Match(1, outcome: PredictionOutcome.Incorrect),
                Match(1, outcome: PredictionOutcome.Pending)
            ]);

        // Act
        var summary = (await HandleAsync()).Single().OutcomeSummary;

        // Assert - a match still to be scored counts towards nothing.
        summary.Should().NotBeNull();
        summary!.ExactScoreCount.Should().Be(1);
        summary.CorrectResultCount.Should().Be(1);
        summary.IncorrectCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotSummariseOutcomes_WhileTheRoundIsStillOpen()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1), hasUserPredicted: true)],
            matches: [Match(1, outcome: PredictionOutcome.ExactScore)]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert - the scoring is provisional until the round closes.
        round.OutcomeSummary.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNotSummariseOutcomes_ForAPlayerWhoDidNotPredict()
    {
        // Arrange - the round is still open, so it appears, but this player has predicted nothing in it.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1), hasUserPredicted: false)],
            matches: [Match(1, outcome: PredictionOutcome.ExactScore)]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert - a summary of no predictions is three zeroes pretending to be a result.
        round.OutcomeSummary.Should().BeNull();
    }

    #endregion

    #region The matches themselves

    [Fact]
    public async Task Handle_ShouldReturnNoMatches_WhenTheRoundHasNone()
    {
        // Arrange - possible while the fixtures are still being loaded.
        Given(rounds: [Round(1, deadlineUtc: Now.AddHours(1))]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldGroupMatchesOntoTheirOwnRound()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1)), Round(2, deadlineUtc: Now.AddHours(2))],
            matches: [Match(1), Match(2), Match(2)]);

        // Act
        var rounds = (await HandleAsync()).ToList();

        // Assert
        rounds.Single(round => round.Id == 1).Matches.Should().HaveCount(1);
        rounds.Single(round => round.Id == 2).Matches.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldOrderMatchesByKickOffThenHomeTeam()
    {
        // Arrange - two matches kicking off together, and one earlier.
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(5))],
            matches:
            [
                Match(1, kickOffUtc: Now.AddHours(3), homeTeamShortName: "WOL"),
                Match(1, kickOffUtc: Now.AddHours(3), homeTeamShortName: "ARS"),
                Match(1, kickOffUtc: Now.AddHours(1), homeTeamShortName: "MUN")
            ]);

        // Act
        var matches = (await HandleAsync()).Single().Matches.ToList();

        // Assert - the home team breaks a tie so a simultaneous pair reads the same way every time.
        matches.Select(match => match.MatchDateTimeUtc).Should().Equal(
            Now.AddHours(1), Now.AddHours(3), Now.AddHours(3));
    }

    [Fact]
    public async Task Handle_ShouldCarryTheMatchDetailsThrough()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1))],
            matches:
            [
                Match(1, status: MatchStatus.InProgress, areTeamsConfirmed: false, matchNumber: 7)
            ]);

        // Act
        var match = (await HandleAsync()).Single().Matches.Single();

        // Assert
        match.Status.Should().Be(MatchStatus.InProgress);
        match.AreTeamsConfirmed.Should().BeFalse();
        match.MatchNumber.Should().Be(7);
    }

    #endregion

    #region The round itself

    [Fact]
    public async Task Handle_ShouldFlagATournamentRound()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1), competitionType: CompetitionType.Tournament)],
            matches: [Match(1)]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.IsTournament.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotFlagALeagueRoundAsATournament()
    {
        // Arrange
        Given(
            rounds: [Round(1, deadlineUtc: Now.AddHours(1), competitionType: CompetitionType.League)],
            matches: [Match(1)]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.IsTournament.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheRoundDetailsThrough()
    {
        // Arrange
        var deadline = Now.AddHours(1);
        Given(
            rounds: [Round(1, deadlineUtc: deadline, status: RoundStatus.InProgress)],
            matches: [Match(1)]);

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.Status.Should().Be(RoundStatus.InProgress);
        round.SeasonName.Should().Be("2026/27");
        round.RoundNumber.Should().Be(1);
        round.DeadlineUtc.Should().Be(deadline);
    }

    #endregion

    private void Given(
        IReadOnlyList<ActiveRoundCandidateRow>? rounds = null,
        IReadOnlyList<ActiveRoundMatchRow>? matches = null)
    {
        _activeRoundsQuery
            .ExecuteAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new ActiveRoundsData(rounds ?? [], matches ?? []));
    }

    private async Task<IEnumerable<ActiveRoundDto>> HandleAsync() =>
        await _handler.Handle(new GetActiveRoundsQuery(UserId), CancellationToken.None);

    private static ActiveRoundCandidateRow Round(
        int roundId,
        DateTime deadlineUtc,
        RoundStatus status = RoundStatus.Published,
        CompetitionType competitionType = CompetitionType.League,
        bool hasUserPredicted = false,
        bool hasConfirmedMatch = true) =>
        new(
            roundId,
            "2026/27",
            roundId,
            null,
            deadlineUtc,
            status,
            competitionType,
            hasUserPredicted,
            hasConfirmedMatch);

    private static ActiveRoundMatchRow Match(
        int roundId,
        DateTime? customLockTimeUtc = null,
        DateTime? kickOffUtc = null,
        string? homeTeamShortName = null,
        PredictionOutcome? outcome = null,
        MatchStatus status = MatchStatus.Scheduled,
        bool areTeamsConfirmed = true,
        int? matchNumber = null,
        int homeCount = 0,
        int drawCount = 0,
        int awayCount = 0) =>
        new(
            roundId,
            null,
            null,
            homeTeamShortName ?? "ARS",
            null,
            null,
            outcome,
            status,
            null,
            null,
            kickOffUtc ?? Now.AddHours(2),
            matchNumber,
            areTeamsConfirmed,
            null,
            null,
            homeCount,
            drawCount,
            awayCount,
            customLockTimeUtc);
}
