using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using Xunit;
using static ThePredictions.Application.Features.Rounds.Queries.GetRoundCompletionQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Rounds.Queries;

/// <summary>
/// The "who still needs to predict?" view. Two audiences share it: a site admin looking across every
/// league in the season, and a league member looking at their own league - where only the owner or an
/// admin may go on to send a reminder. The ordering is deliberate: the people worth chasing first.
/// </summary>
public class GetRoundCompletionQueryHandlerTests
{
    private const int RoundId = 43;
    private const int LeagueId = 10;

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DeadlineUtc = new(2026, 7, 6, 18, 30, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetRoundCompletionQueryHandler _handler;

    public GetRoundCompletionQueryHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _handler = new GetRoundCompletionQueryHandler(_dbConnection, _membershipService, _dateTimeProvider);
    }

    private void GivenRound(string roundName = "Gameweek 5") =>
        _dbConnection.QuerySingleOrDefaultAsync<RoundInfoRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new RoundInfoRow(roundName, DeadlineUtc));

    private void GivenPredictableMatchCount(int count) =>
        _dbConnection.QuerySingleOrDefaultAsync<int>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(count);

    private void GivenParticipants(params ParticipantRow[] participants) =>
        _dbConnection.QueryAsync<ParticipantRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(participants);

    private void GivenMissingFixtures(params MissingFixtureRow[] fixtures) =>
        _dbConnection.QueryAsync<MissingFixtureRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(fixtures);

    private static ParticipantRow Participant(string userId, string name, int predictedCount, DateTime? lastRemindedUtc = null) =>
        new(userId, name, $"{userId}@example.com", predictedCount, lastRemindedUtc);

    private static MissingFixtureRow MissingFixture(string userId, int matchId, int? matchNumber) =>
        new(userId, matchId, matchNumber, $"Home {matchId}", $"Away {matchId}");

    private Task<Contracts.Rounds.RoundCompletionDto?> HandleAsync(int? leagueId = LeagueId, bool isSiteAdmin = true) =>
        _handler.Handle(new GetRoundCompletionQuery(RoundId, leagueId, "user-x", isSiteAdmin), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldThrowUnauthorised_WhenGlobalViewRequestedByNonAdmin()
    {
        var act = () => HandleAsync(leagueId: null, isSiteAdmin: false);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldEnforceMembership_WhenLeagueViewRequestedByNonMember()
    {
        _membershipService.EnsureApprovedMemberAsync(LeagueId, "user-x", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        var act = () => HandleAsync(isSiteAdmin: false);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldAllowRemindersAndSkipMembershipChecks_WhenAnAdminViewsEveryLeague()
    {
        GivenRound();

        var result = await HandleAsync(leagueId: null);

        result!.CanSendReminders.Should().BeTrue();
        await _membershipService.DidNotReceiveWithAnyArgs()
            .EnsureApprovedMemberAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAllowReminders_WhenTheViewerOwnsTheLeague()
    {
        GivenRound();
        _membershipService.IsLeagueAdministratorAsync(LeagueId, "user-x", Arg.Any<CancellationToken>()).Returns(true);

        var result = await HandleAsync(isSiteAdmin: false);

        result!.CanSendReminders.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRefuseReminders_WhenTheViewerIsAnOrdinaryMember()
    {
        // An approved member may see who is missing; only the owner or an admin may chase them.
        GivenRound();
        _membershipService.IsLeagueAdministratorAsync(LeagueId, "user-x", Arg.Any<CancellationToken>()).Returns(false);

        var result = await HandleAsync(isSiteAdmin: false);

        result!.CanSendReminders.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotCheckLeagueOwnership_WhenTheViewerIsASiteAdmin()
    {
        GivenRound();

        var result = await HandleAsync();

        result!.CanSendReminders.Should().BeTrue();
        await _membershipService.DidNotReceiveWithAnyArgs()
            .IsLeagueAdministratorAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheRoundDoesNotExist()
    {
        var result = await HandleAsync();

        result.Should().BeNull();
        await _dbConnection.DidNotReceiveWithAnyArgs().QueryAsync<ParticipantRow>(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoundNameAndDeadline()
    {
        GivenRound("Semi-finals");

        var result = await HandleAsync();

        result!.RoundId.Should().Be(RoundId);
        result.RoundName.Should().Be("Semi-finals");
        result.DeadlineUtc.Should().Be(DeadlineUtc);
    }

    [Fact]
    public async Task Handle_ShouldReportTheDeadlineAsPassed_WhenNothingIsStillPredictable()
    {
        // Every fixture has locked, so there is nothing left for anyone to enter.
        GivenRound();
        GivenPredictableMatchCount(0);

        var result = await HandleAsync();

        result!.DeadlinePassed.Should().BeTrue();
        result.PredictableMatchCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportTheDeadlineAsOpen_WhileFixturesRemainPredictable()
    {
        GivenRound();
        GivenPredictableMatchCount(6);

        var result = await HandleAsync();

        result!.DeadlinePassed.Should().BeFalse();
        result.PredictableMatchCount.Should().Be(6);
    }

    [Fact]
    public async Task Handle_ShouldListEachPlayerWithTheirFixturesStillOutstanding()
    {
        GivenRound();
        GivenPredictableMatchCount(3);
        GivenParticipants(Participant("u1", "Alice A", predictedCount: 1, lastRemindedUtc: NowUtc.AddHours(-2)));
        GivenMissingFixtures(MissingFixture("u1", 501, 2), MissingFixture("u1", 502, 3));

        var result = await HandleAsync();

        var player = result!.Players.Should().ContainSingle().Subject;
        player.UserId.Should().Be("u1");
        player.PlayerName.Should().Be("Alice A");
        player.Email.Should().Be("u1@example.com");
        player.PredictedCount.Should().Be(1);
        player.LastRemindedUtc.Should().Be(NowUtc.AddHours(-2));
        player.MissingFixtures.Select(f => f.MatchId).Should().Equal(501, 502);
    }

    [Fact]
    public async Task Handle_ShouldListOutstandingFixturesInMatchOrder()
    {
        // The list mirrors the prediction screen, so it has to follow the fixture numbering rather
        // than whatever order the rows arrived in.
        GivenRound();
        GivenParticipants(Participant("u1", "Alice A", predictedCount: 0));
        GivenMissingFixtures(
            MissingFixture("u1", 503, 3),
            MissingFixture("u1", 501, 1),
            MissingFixture("u1", 502, 2));

        var result = await HandleAsync();

        result!.Players.Single().MissingFixtures.Select(f => f.MatchId).Should().Equal(501, 502, 503);
    }

    [Fact]
    public async Task Handle_ShouldGiveEachPlayerOnlyTheirOwnOutstandingFixtures()
    {
        GivenRound();
        GivenParticipants(
            Participant("u1", "Alice A", predictedCount: 0),
            Participant("u2", "Bob B", predictedCount: 0));
        GivenMissingFixtures(MissingFixture("u1", 501, 1), MissingFixture("u2", 502, 2));

        var result = await HandleAsync();

        result!.Players.Single(p => p.UserId == "u1").MissingFixtures.Single().MatchId.Should().Be(501);
        result.Players.Single(p => p.UserId == "u2").MissingFixtures.Single().MatchId.Should().Be(502);
    }

    [Fact]
    public async Task Handle_ShouldLeaveAPlayerWhoIsUpToDateWithNothingOutstanding()
    {
        GivenRound();
        GivenParticipants(Participant("u1", "Alice A", predictedCount: 3));

        var result = await HandleAsync();

        var player = result!.Players.Should().ContainSingle().Subject;
        player.MissingFixtures.Should().BeEmpty();
        player.IsPartial.Should().BeFalse();
        player.HasEnteredNothing.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPutTheHalfFinishedPlayersFirstThenThoseWhoHaveEnteredNothing()
    {
        // Someone mid-way through is the most likely to finish if nudged, so they lead; players who
        // are already done need no chasing and sink to the bottom.
        GivenRound();
        GivenParticipants(
            Participant("done", "Zoe Z", predictedCount: 2),
            Participant("nothing", "Bob B", predictedCount: 0),
            Participant("partial", "Yan Y", predictedCount: 1));
        GivenMissingFixtures(MissingFixture("nothing", 501, 1), MissingFixture("partial", 502, 2));

        var result = await HandleAsync();

        result!.Players.Select(p => p.UserId).Should().Equal("partial", "nothing", "done");
    }

    [Fact]
    public async Task Handle_ShouldOrderPlayersByNameWithinTheSameStanding()
    {
        GivenRound();
        GivenParticipants(
            Participant("u3", "Carla C", predictedCount: 0),
            Participant("u1", "Alice A", predictedCount: 0),
            Participant("u2", "Bob B", predictedCount: 0));
        GivenMissingFixtures(
            MissingFixture("u1", 501, 1),
            MissingFixture("u2", 501, 1),
            MissingFixture("u3", 501, 1));

        var result = await HandleAsync();

        result!.Players.Select(p => p.PlayerName).Should().Equal("Alice A", "Bob B", "Carla C");
    }

    [Fact]
    public async Task Handle_ShouldScopeEveryQueryToTheRoundAndItsDeadline()
    {
        // The predictable-fixture predicate depends on the round deadline and the current time, so
        // both have to reach the SQL alongside the round and league being asked about.
        GivenRound();

        await HandleAsync();

        await _dbConnection.Received(1).QueryAsync<ParticipantRow>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Is<object>(p => HasParameters(p)));
    }

    private static bool HasParameters(object parameters)
    {
        var type = parameters.GetType();
        return (int)type.GetProperty("RoundId")!.GetValue(parameters)! == RoundId
               && (int?)type.GetProperty("LeagueId")!.GetValue(parameters)! == LeagueId
               && (DateTime)type.GetProperty("NowUtc")!.GetValue(parameters)! == NowUtc
               && (DateTime)type.GetProperty("RoundDeadlineUtc")!.GetValue(parameters)! == DeadlineUtc
               && (string)type.GetProperty("ApprovedStatus")!.GetValue(parameters)! == "Approved"
               && (string)type.GetProperty("ScheduledStatus")!.GetValue(parameters)! == "Scheduled";
    }
}
