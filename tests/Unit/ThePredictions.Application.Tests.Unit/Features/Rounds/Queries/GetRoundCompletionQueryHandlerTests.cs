using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Rounds.Queries;

/// <summary>
/// Round completion: who is up to date and who is missing what.
///
/// These tests used to stub the predictable-fixture count, because the handler asked the database for it
/// through a SQL predicate. Now they supply fixtures and let the handler decide, so the rule
/// (<c>Match.IsOpenForPrediction</c>) is exercised here rather than mocked away - which is the point of the
/// persistence split: what was a stubbed number is now real behaviour under test.
/// </summary>
public class GetRoundCompletionQueryHandlerTests
{
    private const int RoundId = 7;
    private const int LeagueId = 42;
    private const string ViewerId = "user-x";

    private static readonly DateTime NowUtc = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DeadlineUtc = NowUtc.AddDays(2);

    private readonly IRoundCompletionQuery _completionQuery = Substitute.For<IRoundCompletionQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetRoundCompletionQueryHandler _handler;

    public GetRoundCompletionQueryHandlerTests()
    {
        _handler = new GetRoundCompletionQueryHandler(
            _completionQuery, _membershipService, new TestDateTimeProvider(NowUtc));
    }

    #region Authorisation

    [Fact]
    public async Task Handle_ShouldThrowUnauthorised_WhenGlobalViewRequestedByNonAdmin()
    {
        var act = () => HandleAsync(leagueId: null, isSiteAdmin: false);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldEnforceMembership_WhenLeagueViewRequestedByNonMember()
    {
        _membershipService.EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldAllowRemindersAndSkipMembershipChecks_WhenAnAdminViewsEveryLeague()
    {
        Given();

        var result = await HandleAsync(leagueId: null, isSiteAdmin: true);

        result.CanSendReminders.Should().BeTrue();
        await _membershipService.DidNotReceiveWithAnyArgs()
            .EnsureApprovedMemberAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAllowReminders_WhenTheViewerOwnsTheLeague()
    {
        Given();
        _membershipService.IsLeagueAdministratorAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>()).Returns(true);

        (await HandleAsync(isSiteAdmin: false)).CanSendReminders.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRefuseReminders_WhenTheViewerIsAnOrdinaryMember()
    {
        Given();
        _membershipService.IsLeagueAdministratorAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>()).Returns(false);

        (await HandleAsync(isSiteAdmin: false)).CanSendReminders.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotCheckLeagueOwnership_WhenTheViewerIsASiteAdmin()
    {
        Given();

        (await HandleAsync(isSiteAdmin: true)).CanSendReminders.Should().BeTrue();
        await _membershipService.DidNotReceiveWithAnyArgs()
            .IsLeagueAdministratorAsync(default, default!, CancellationToken.None);
    }

    #endregion

    #region The round itself

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenTheRoundDoesNotExist()
    {
        _completionQuery.ExecuteAsync(RoundId, LeagueId, Arg.Any<CancellationToken>())
            .Returns((RoundCompletionData?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoundNameAndDeadline()
    {
        Given(displayName: "Gameweek 5");

        var result = await HandleAsync();

        result.RoundName.Should().Be("Gameweek 5");
        result.DeadlineUtc.Should().Be(DeadlineUtc);
        result.RoundId.Should().Be(RoundId);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheRoundNumber_WhenTheRoundHasNoDisplayName()
    {
        // The naming rule is Round.GetDisplayNameOrDefault, previously a CASE expression in the SQL.
        Given(displayName: "   ");

        (await HandleAsync()).RoundName.Should().Be($"Round {RoundId}");
    }

    [Fact]
    public async Task Handle_ShouldPassTheRoundAndLeagueToThePort()
    {
        Given();

        await HandleAsync();

        await _completionQuery.Received(1).ExecuteAsync(RoundId, LeagueId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region What still counts as predictable

    [Fact]
    public async Task Handle_ShouldCountOnlyFixturesStillOpen_WhenSomeHaveLockedOrKickedOff()
    {
        // The rule runs here now: only the first of these four can still be predicted.
        Given(fixtures:
        [
            Fixture(1),
            Fixture(2, customLock: NowUtc.AddHours(-1)),
            Fixture(3, status: MatchStatus.InProgress),
            Fixture(4, homeTeamId: null)
        ]);

        (await HandleAsync()).PredictableMatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReportTheDeadlineAsPassed_WhenNothingIsStillPredictable()
    {
        Given(fixtures: [Fixture(1, status: MatchStatus.Completed)]);

        var result = await HandleAsync();

        result.DeadlinePassed.Should().BeTrue();
        result.PredictableMatchCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportTheDeadlineAsOpen_WhileFixturesRemainPredictable()
    {
        Given(fixtures: [Fixture(1)]);

        (await HandleAsync()).DeadlinePassed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldTreatAFixtureWithALaterCustomLockAsOpen_EvenAfterTheRoundDeadline()
    {
        // A combined round: its final locks later than the deadline that closed the semi-finals.
        Given(deadlineUtc: NowUtc.AddHours(-24), fixtures: [Fixture(1, customLock: NowUtc.AddHours(6))]);

        var result = await HandleAsync();

        result.PredictableMatchCount.Should().Be(1);
        result.DeadlinePassed.Should().BeFalse("the round is still open even though its deadline has gone.");
    }

    #endregion

    #region Players

    [Fact]
    public async Task Handle_ShouldListEachPlayerWithTheirFixturesStillOutstanding()
    {
        Given(
            fixtures: [Fixture(1), Fixture(2)],
            participants: [Participant("u1", "Ada", "Lovelace")],
            predictions: [new RoundPredictionRow("u1", 1)]);

        var player = (await HandleAsync()).Players.Single();

        player.PlayerName.Should().Be("Ada L");
        player.Email.Should().Be("u1@example.com");
        player.PredictedCount.Should().Be(1);
        player.MissingFixtures.Select(f => f.MatchId).Should().Equal(2);
    }

    [Fact]
    public async Task Handle_ShouldListOutstandingFixturesInMatchOrder()
    {
        Given(
            fixtures: [Fixture(3, matchNumber: 3), Fixture(1, matchNumber: 1), Fixture(2, matchNumber: 2)],
            participants: [Participant("u1", "Ada", "Lovelace")]);

        (await HandleAsync()).Players.Single()
            .MissingFixtures.Select(f => f.MatchNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldGiveEachPlayerOnlyTheirOwnOutstandingFixtures()
    {
        Given(
            fixtures: [Fixture(1), Fixture(2)],
            participants: [Participant("u1", "Ada", "Lovelace"), Participant("u2", "Grace", "Hopper")],
            predictions: [new RoundPredictionRow("u1", 1), new RoundPredictionRow("u2", 2)]);

        var players = (await HandleAsync()).Players;

        players.Single(p => p.UserId == "u1").MissingFixtures.Select(f => f.MatchId).Should().Equal(2);
        players.Single(p => p.UserId == "u2").MissingFixtures.Select(f => f.MatchId).Should().Equal(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveAPlayerWhoIsUpToDateWithNothingOutstanding()
    {
        Given(
            fixtures: [Fixture(1)],
            participants: [Participant("u1", "Ada", "Lovelace")],
            predictions: [new RoundPredictionRow("u1", 1)]);

        var player = (await HandleAsync()).Players.Single();

        player.MissingFixtures.Should().BeEmpty();
        player.PredictedCount.Should().Be(1);
        player.IsPartial.Should().BeFalse();
        player.HasEnteredNothing.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAPredictionForAFixtureNoLongerPredictable()
    {
        // A prediction against a locked fixture must not inflate the predicted count, or a player who has
        // done nothing about the open fixtures would look half-finished.
        Given(
            fixtures: [Fixture(1), Fixture(2, status: MatchStatus.Completed)],
            participants: [Participant("u1", "Ada", "Lovelace")],
            predictions: [new RoundPredictionRow("u1", 2)]);

        var player = (await HandleAsync()).Players.Single();

        player.PredictedCount.Should().Be(0);
        player.HasEnteredNothing.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPutTheHalfFinishedPlayersFirstThenThoseWhoHaveEnteredNothing()
    {
        Given(
            fixtures: [Fixture(1), Fixture(2)],
            participants:
            [
                Participant("done", "Zoe", "Zeta"),
                Participant("nothing", "Yan", "Yates"),
                Participant("partial", "Xu", "Xiang")
            ],
            predictions:
            [
                new RoundPredictionRow("done", 1), new RoundPredictionRow("done", 2),
                new RoundPredictionRow("partial", 1)
            ]);

        (await HandleAsync()).Players.Select(p => p.UserId).Should().Equal("partial", "nothing", "done");
    }

    [Fact]
    public async Task Handle_ShouldOrderPlayersByNameWithinTheSameStanding()
    {
        Given(
            fixtures: [Fixture(1)],
            participants:
            [
                Participant("u2", "Grace", "Hopper"),
                Participant("u1", "Ada", "Lovelace")
            ]);

        (await HandleAsync()).Players.Select(p => p.PlayerName).Should().Equal("Ada L", "Grace H");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheLastReminderTimestamp_WhenOneExists()
    {
        var lastReminded = NowUtc.AddHours(-3);
        Given(participants: [Participant("u1", "Ada", "Lovelace", lastReminded)]);

        (await HandleAsync()).Players.Single().LastRemindedUtc.Should().Be(lastReminded);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToEmptyTeamNames_WhenAFixtureHasNone()
    {
        // A knockout tie can be confirmed without the join yielding names in the same read; the page shows
        // blanks rather than failing.
        Given(fixtures: [Fixture(1)], teamNames: new Dictionary<int, RoundFixtureTeams>());

        var fixture = (await HandleAsync()).Players.Single().MissingFixtures.Single();

        fixture.HomeTeam.Should().BeEmpty();
        fixture.AwayTeam.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private void Given(
        string displayName = "Gameweek 5",
        DateTime? deadlineUtc = null,
        IReadOnlyList<Match>? fixtures = null,
        IReadOnlyList<RoundParticipantRow>? participants = null,
        IReadOnlyList<RoundPredictionRow>? predictions = null,
        IReadOnlyDictionary<int, RoundFixtureTeams>? teamNames = null)
    {
        var matches = fixtures ?? [Fixture(1)];
        var deadline = deadlineUtc ?? DeadlineUtc;

        var round = new Round(
            id: RoundId, seasonId: 1, roundNumber: RoundId, displayName: displayName,
            startDateUtc: deadline.AddDays(-1), deadlineUtc: deadline, status: RoundStatus.Published,
            apiRoundName: null, lastReminderSentUtc: null, matches: matches, resultsDigestSentUtc: null);

        var names = teamNames ?? matches.ToDictionary(
            m => m.Id, m => new RoundFixtureTeams($"Home {m.Id}", $"Away {m.Id}"));

        var data = new RoundCompletionData(
            round,
            names,
            participants ?? [Participant("u1", "Ada", "Lovelace")],
            predictions ?? []);

        _completionQuery.ExecuteAsync(RoundId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(data);
    }

    private static Match Fixture(
        int id,
        DateTime? customLock = null,
        MatchStatus status = MatchStatus.Scheduled,
        int? homeTeamId = 1,
        int? matchNumber = null) =>
        new(
            id: id, roundId: RoundId, homeTeamId: homeTeamId, awayTeamId: 2,
            matchDateTimeUtc: NowUtc.AddDays(3), customLockTimeUtc: customLock, status: status,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: null,
            matchNumber: matchNumber ?? id, placeholderHomeName: null, placeholderAwayName: null,
            apiRoundName: null);

    private static RoundParticipantRow Participant(
        string userId, string firstName, string lastName, DateTime? lastRemindedUtc = null) =>
        new(userId, firstName, lastName, $"{userId}@example.com", lastRemindedUtc);

    private Task<RoundCompletionDto> HandleAsync(int? leagueId = LeagueId, bool isSiteAdmin = true) =>
        _handler.Handle(new GetRoundCompletionQuery(RoundId, leagueId, ViewerId, isSiteAdmin), CancellationToken.None);

    #endregion
}
