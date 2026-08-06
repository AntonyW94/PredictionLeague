using FluentAssertions;
using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// The hinge of the whole scoring pipeline: entering match results moves the round through its
/// statuses, scores everyone's predictions, applies boosts, refreshes the standings and - once the
/// last match is done - triggers prizes, badges and the results emails, in that order.
/// </summary>
public class UpdateMatchResultsCommandHandlerTests
{
    private const int RoundId = 100;
    private const int SeasonId = 7;
    private const int MatchId = 500;

    private static readonly DateTime FixedNow = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IBoostService _boostService = Substitute.For<IBoostService>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IUserPredictionRepository _predictionRepository = Substitute.For<IUserPredictionRepository>();
    private readonly ILeagueStatsRepository _leagueStatsRepository = Substitute.For<ILeagueStatsRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    private readonly UpdateMatchResultsCommandHandler _handler;

    public UpdateMatchResultsCommandHandlerTests()
    {
        _handler = new UpdateMatchResultsCommandHandler(
            _mediator, _boostService, _leagueRepository, _roundRepository,
            _predictionRepository, _leagueStatsRepository, _currentUserService,
            new TestDateTimeProvider(FixedNow));

        _predictionRepository.GetByMatchIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _leagueRepository.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        _mediator.Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private static Match BuildMatch(int id, MatchStatus status) =>
        new(id: id, roundId: RoundId, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: FixedNow.AddDays(-1), customLockTimeUtc: null, status: status,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: null,
            matchNumber: 1, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    private Round GivenRound(RoundStatus status, params Match[] matches)
    {
        var round = new Round(
            id: RoundId, seasonId: SeasonId, roundNumber: 5, displayName: "Round 5",
            startDateUtc: FixedNow.AddDays(-2), deadlineUtc: FixedNow.AddDays(-1),
            status: status, apiRoundName: null, lastReminderSentUtc: null,
            matches: matches.Length == 0 ? [BuildMatch(MatchId, MatchStatus.Scheduled)] : matches);

        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns(round);
        return round;
    }

    private static UpdateMatchResultsCommand Command(params MatchResultDto[] results) =>
        new(RoundId, results.Length == 0 ? [new MatchResultDto(MatchId, 2, 1, MatchStatus.Completed)] : results.ToList());

    private Task HandleAsync(UpdateMatchResultsCommand? command = null) =>
        _handler.Handle(command ?? Command(), CancellationToken.None);

    // ---------- authorisation ----------

    [Fact]
    public async Task Handle_ShouldRequireAdministrator_WhenCalledBySignedInUser()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        GivenRound(RoundStatus.InProgress);

        await HandleAsync();

        _currentUserService.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldSkipTheAdminCheck_ForTheScheduledTask()
    {
        // The score-update job authenticates with an API key, not a user, so there is no admin to
        // check - insisting on one would break the every-minute score refresh.
        _currentUserService.IsAuthenticated.Returns(false);
        GivenRound(RoundStatus.InProgress);

        await HandleAsync();

        _currentUserService.DidNotReceive().EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheRoundDoesNotExist()
    {
        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns((Round?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---------- matching results to matches ----------

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoResultMatchesARealFixture()
    {
        GivenRound(RoundStatus.InProgress);

        await HandleAsync(Command(new MatchResultDto(MatchId + 999, 1, 0, MatchStatus.Completed)));

        await _roundRepository.DidNotReceiveWithAnyArgs().UpdateMatchScoresAsync(default!, CancellationToken.None);
        await _leagueStatsRepository.DidNotReceiveWithAnyArgs().RefreshSeasonAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldApplyTheScoreToTheMatchingFixture()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(new MatchResultDto(MatchId, 3, 1, MatchStatus.Completed)));

        match.ActualHomeTeamScore.Should().Be(3);
        match.ActualAwayTeamScore.Should().Be(1);
        match.Status.Should().Be(MatchStatus.Completed);
        await _roundRepository.Received(1).UpdateMatchScoresAsync(
            Arg.Is<List<Match>>(m => m.Single().Id == MatchId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldIgnoreResultsForFixturesNotInTheRound()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(
            new MatchResultDto(MatchId, 1, 0, MatchStatus.Completed),
            new MatchResultDto(MatchId + 999, 4, 4, MatchStatus.Completed)));

        await _roundRepository.Received(1).UpdateMatchScoresAsync(
            Arg.Is<List<Match>>(m => m.Count == 1), Arg.Any<CancellationToken>());
    }

    // ---------- the round starting ----------

    [Fact]
    public async Task Handle_ShouldMoveAPublishedRoundToInProgress_OnceAMatchIsUnderway()
    {
        var kickedOff = BuildMatch(MatchId, MatchStatus.Scheduled);
        var later = BuildMatch(MatchId + 1, MatchStatus.Scheduled);
        var round = GivenRound(RoundStatus.Published, kickedOff, later);

        await HandleAsync(Command(new MatchResultDto(MatchId, 0, 0, MatchStatus.InProgress)));

        round.Status.Should().Be(RoundStatus.InProgress);
    }

    [Fact]
    public async Task Handle_ShouldNotReopenARoundThatHasAlreadyStarted()
    {
        var match = BuildMatch(MatchId, MatchStatus.InProgress);
        GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.InProgress)));

        await _roundRepository.DidNotReceive().IsLastRoundOfSeasonAsync(
            RoundId, SeasonId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAutoApplyUnusedBoosts_WhenTheFinalRoundStarts()
    {
        // Last chance to use a boost, so anyone still holding one has it applied automatically.
        var kickedOff = BuildMatch(MatchId, MatchStatus.Scheduled);
        var later = BuildMatch(MatchId + 1, MatchStatus.Scheduled);
        GivenRound(RoundStatus.Published, kickedOff, later);
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        await HandleAsync(Command(new MatchResultDto(MatchId, 0, 0, MatchStatus.InProgress)));

        await _boostService.Received(1).AutoApplyUnusedBoostsForLastRoundAsync(RoundId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotAutoApplyBoosts_WhenAnOrdinaryRoundStarts()
    {
        var kickedOff = BuildMatch(MatchId, MatchStatus.Scheduled);
        var later = BuildMatch(MatchId + 1, MatchStatus.Scheduled);
        GivenRound(RoundStatus.Published, kickedOff, later);
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);

        await HandleAsync(Command(new MatchResultDto(MatchId, 0, 0, MatchStatus.InProgress)));

        await _boostService.DidNotReceiveWithAnyArgs().AutoApplyUnusedBoostsForLastRoundAsync(default, CancellationToken.None);
    }

    // ---------- scoring predictions ----------

    [Fact]
    public async Task Handle_ShouldScoreThePredictionsForTheUpdatedMatches()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);
        var prediction = UserPrediction.Create("user-1", MatchId, 3, 1, new TestDateTimeProvider(FixedNow));
        _predictionRepository.GetByMatchIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns([prediction]);

        await HandleAsync(Command(new MatchResultDto(MatchId, 3, 1, MatchStatus.Completed)));

        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
        await _predictionRepository.Received(1).UpdateOutcomesAsync(
            Arg.Is<IEnumerable<UserPrediction>>(p => p.Contains(prediction)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveAPredictionAlone_WhenItsMatchWasNotUpdated()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);
        var strayPrediction = UserPrediction.Create("user-1", MatchId + 999, 3, 1, new TestDateTimeProvider(FixedNow));
        _predictionRepository.GetByMatchIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns([strayPrediction]);

        await HandleAsync(Command(new MatchResultDto(MatchId, 3, 1, MatchStatus.Completed)));

        strayPrediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    // ---------- the standings refresh ----------

    [Fact]
    public async Task Handle_ShouldRefreshTheStandingsAfterApplyingBoosts()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.InProgress)));

        Received.InOrder(() =>
        {
            _boostService.ApplyRoundBoostsAsync(RoundId, Arg.Any<CancellationToken>());
            _leagueStatsRepository.RefreshSeasonAsync(SeasonId, Arg.Any<CancellationToken>());
        });
    }

    // ---------- the round finishing ----------

    [Fact]
    public async Task Handle_ShouldNotCompleteTheRound_WhileAMatchIsStillToPlay()
    {
        var finished = BuildMatch(MatchId, MatchStatus.Scheduled);
        var stillToPlay = BuildMatch(MatchId + 1, MatchStatus.Scheduled);
        var round = GivenRound(RoundStatus.InProgress, finished, stillToPlay);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.Completed)));

        round.Status.Should().Be(RoundStatus.InProgress);
        await _mediator.DidNotReceive().Send(Arg.Any<SendRoundDigestEmailsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCompleteTheRound_WhenEveryMatchIsDone()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        var round = GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.Completed)));

        round.Status.Should().Be(RoundStatus.Completed);
    }

    [Fact]
    public async Task Handle_ShouldCompleteTheRound_WhenTheLastMatchIsPostponedRatherThanPlayed()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        var round = GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(new MatchResultDto(MatchId, 0, 0, MatchStatus.Postponed)));

        round.Status.Should().Be(RoundStatus.Completed);
    }

    [Fact]
    public async Task Handle_ShouldProcessPrizesForEveryLeagueInTheSeason()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);
        _leagueRepository.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([11, 22]);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.Completed)));

        await _mediator.Received(1).Send(
            Arg.Is<ProcessPrizesCommand>(c => c.LeagueId == 11 && c.RoundId == RoundId), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<ProcessPrizesCommand>(c => c.LeagueId == 22 && c.RoundId == RoundId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSettlePrizesAndBadgesBeforeSendingTheDigest()
    {
        // The digest email reports each player's new position and celebrates the badges they just
        // earned, so it has to go out after both are finalised - and the prize emails after it.
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.Completed)));

        Received.InOrder(() =>
        {
            _leagueStatsRepository.RefreshSeasonAsync(SeasonId, Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<SendRoundDigestEmailsCommand>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<SendPrizeNotificationsCommand>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ShouldPassTheNewBadgesToTheDigest()
    {
        var match = BuildMatch(MatchId, MatchStatus.Scheduled);
        GivenRound(RoundStatus.InProgress, match);
        IReadOnlyList<RoundBadgeAward> awarded = [new("user-1", "marksman-1")];
        _mediator.Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>()).Returns(awarded);

        await HandleAsync(Command(new MatchResultDto(MatchId, 1, 0, MatchStatus.Completed)));

        await _mediator.Received(1).Send(
            Arg.Is<SendRoundDigestEmailsCommand>(c => c.BadgesAwarded == awarded), Arg.Any<CancellationToken>());
    }
}
