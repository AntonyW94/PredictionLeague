using FluentAssertions;
using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// The every-minute live-score tick. It refreshes the cached dashboard ranks unconditionally, then
/// pulls results for any match that has kicked off but is not finished - bailing out early at each
/// step so a quiet minute costs almost nothing.
/// </summary>
public class UpdateScoresForNextRoundFlowTests
{
    private const int SeasonId = 11;
    private const int RoundId = 100;
    private const int CompetitionId = 3;

    private static readonly DateTime KickedOff = DateTime.UtcNow.AddHours(-1);
    private static readonly DateTime NotYetPlayed = DateTime.UtcNow.AddDays(1);

    private readonly IRoundRepository _rounds = Substitute.For<IRoundRepository>();
    private readonly ISeasonRepository _seasons = Substitute.For<ISeasonRepository>();
    private readonly ICompetitionRepository _competitions = Substitute.For<ICompetitionRepository>();
    private readonly IFootballDataService _footballData = Substitute.For<IFootballDataService>();
    private readonly ILeagueStatsRepository _leagueStats = Substitute.For<ILeagueStatsRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private readonly UpdateScoresForNextRoundCommandHandler _handler;

    public UpdateScoresForNextRoundFlowTests()
    {
        _handler = new UpdateScoresForNextRoundCommandHandler(
            _rounds, _seasons, _competitions, _footballData, _leagueStats, _mediator);

        _footballData.GetFixturesByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>()).Returns([]);
        GivenSeasonAndCompetition();
    }

    private static Match Match(int id, DateTime kickOffUtc, MatchStatus status, int? externalId, string? apiRoundName = null) =>
        new(id: id, roundId: RoundId, homeTeamId: 101, awayTeamId: 102, matchDateTimeUtc: kickOffUtc,
            customLockTimeUtc: null, status: status, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: externalId, matchNumber: 1, placeholderHomeName: null, placeholderAwayName: null,
            apiRoundName: apiRoundName);

    private void GivenActiveRound(params Match[] matches) =>
        _rounds.GetOldestInProgressRoundAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(
            new Round(id: RoundId, seasonId: SeasonId, roundNumber: 5, displayName: "Round 5",
                startDateUtc: KickedOff.AddDays(-1), deadlineUtc: KickedOff.AddDays(-1).AddMinutes(-30),
                status: RoundStatus.InProgress, apiRoundName: null, lastReminderSentUtc: null,
                matches: matches.Length == 0 ? null : matches));

    private void GivenNoActiveRound() =>
        _rounds.GetOldestInProgressRoundAsync(SeasonId, Arg.Any<CancellationToken>()).Returns((Round?)null);

    private void GivenSeasonAndCompetition(bool isTournament = false, bool seasonExists = true)
    {
        _seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(seasonExists
            ? new Season(id: SeasonId, name: "2026/27", startDateUtc: KickedOff.AddMonths(-2),
                endDateUtc: KickedOff.AddMonths(6), isActive: true, numberOfRounds: 38,
                competitionId: CompetitionId, passStandardPrice: null, passPremiumPrice: null)
            : null);

        _competitions.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns(
            new Competition(id: CompetitionId, code: "UCL", name: "Champions League",
                type: isTournament ? CompetitionType.Tournament : CompetitionType.League,
                logoUrl: null, description: null, apiLeagueId: 2, createdAtUtc: KickedOff.AddYears(-1)));
    }

    private void GivenLiveFixtures(params FixtureResponse[] fixtures) =>
        _footballData.GetFixturesByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(fixtures.ToList());

    private static FixtureResponse LiveFixture(int externalId, int homeGoals, int awayGoals, string status = "FT", bool withGoals = true) =>
        new()
        {
            Fixture = new Fixture { Id = externalId, Date = DateTimeOffset.UtcNow, Status = new Status { Short = status } },
            Goals = withGoals ? new Goals { Home = homeGoals, Away = awayGoals } : null
        };

    private Task HandleAsync() => _handler.Handle(new UpdateScoresForNextRoundCommand(SeasonId), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRefreshTheDashboardRanksEvenWhenNothingIsLive()
    {
        // The tile changes with the passage of time - a round ageing out of its window - and nothing
        // else writes when that happens, so the refresh must not be conditional.
        GivenNoActiveRound();

        await HandleAsync();

        await _leagueStats.Received(1).RefreshSeasonAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenThereIsNoRoundInProgress()
    {
        GivenNoActiveRound();

        await HandleAsync();

        await _footballData.DidNotReceiveWithAnyArgs().GetFixturesByIdsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenTheRoundHasNoMatches()
    {
        GivenActiveRound();

        await HandleAsync();

        await _footballData.DidNotReceiveWithAnyArgs().GetFixturesByIdsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenNoMatchHasKickedOffYet()
    {
        GivenActiveRound(Match(1, NotYetPlayed, MatchStatus.Scheduled, externalId: 5001));

        await HandleAsync();

        await _footballData.DidNotReceiveWithAnyArgs().GetFixturesByIdsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenEveryKickedOffMatchIsAlreadyFinished()
    {
        GivenActiveRound(Match(1, KickedOff, MatchStatus.Completed, externalId: 5001));

        await HandleAsync();

        await _footballData.DidNotReceiveWithAnyArgs().GetFixturesByIdsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenNoMatchIsLinkedToTheFeed()
    {
        // A hand-entered fixture has no external id, so there is nothing to look up.
        GivenActiveRound(Match(1, KickedOff, MatchStatus.Scheduled, externalId: null));

        await HandleAsync();

        await _footballData.DidNotReceiveWithAnyArgs().GetFixturesByIdsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenTheFeedReturnsNothing()
    {
        GivenActiveRound(Match(1, KickedOff, MatchStatus.Scheduled, externalId: 5001));
        GivenLiveFixtures();

        await HandleAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateMatchResultsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldOnlyAskAboutMatchesThatHaveStartedAndAreUnfinished()
    {
        GivenActiveRound(
            Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001),
            Match(2, NotYetPlayed, MatchStatus.Scheduled, externalId: 5002),
            Match(3, KickedOff, MatchStatus.Completed, externalId: 5003));

        await HandleAsync();

        await _footballData.Received(1).GetFixturesByIdsAsync(
            Arg.Is<List<int>>(ids => ids.Count == 1 && ids[0] == 5001), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassTheScoresOnForProcessing()
    {
        GivenActiveRound(Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001));
        GivenLiveFixtures(LiveFixture(5001, 2, 1));

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<UpdateMatchResultsCommand>(c =>
                c.RoundId == RoundId
                && c.Matches.Single().MatchId == 1
                && c.Matches.Single().HomeScore == 2
                && c.Matches.Single().AwayScore == 1
                && c.Matches.Single().Status == MatchStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipAFixtureTheFeedSentWithoutAScore()
    {
        GivenActiveRound(Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001));
        GivenLiveFixtures(LiveFixture(5001, 0, 0, withGoals: false));

        await HandleAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateMatchResultsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipAFixtureTheFeedSentWithNoDetailAtAll()
    {
        GivenActiveRound(Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001));
        GivenLiveFixtures(new FixtureResponse { Fixture = null, Goals = new Goals { Home = 1, Away = 0 } });

        await HandleAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateMatchResultsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTreatALeagueMatchInExtraTimeAsStillLive()
    {
        GivenActiveRound(Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001));
        GivenLiveFixtures(LiveFixture(5001, 1, 1, status: "ET"));

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<UpdateMatchResultsCommand>(c => c.Matches.Single().Status == MatchStatus.InProgress),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTreatAKnockoutTieInExtraTimeAsDecided()
    {
        // A knockout tie is scored on the 90-minute result, so once regular time is over the
        // prediction outcome can no longer change.
        GivenSeasonAndCompetition(isTournament: true);
        GivenActiveRound(Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001, apiRoundName: "Semi-finals"));
        GivenLiveFixtures(LiveFixture(5001, 1, 1, status: "ET"));

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<UpdateMatchResultsCommand>(c => c.Matches.Single().Status == MatchStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTreatTheSeasonAsALeague_WhenItCannotBeFound()
    {
        // Defensive: without a season there is no competition to consult, so knockout rules are off.
        GivenSeasonAndCompetition(seasonExists: false);
        GivenActiveRound(Match(1, KickedOff, MatchStatus.InProgress, externalId: 5001));
        GivenLiveFixtures(LiveFixture(5001, 1, 1, status: "ET"));

        await HandleAsync();

        await _competitions.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken.None);
        await _mediator.Received(1).Send(
            Arg.Is<UpdateMatchResultsCommand>(c => c.Matches.Single().Status == MatchStatus.InProgress),
            Arg.Any<CancellationToken>());
    }
}
