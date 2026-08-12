using FluentAssertions;
using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// The admin rebuild button. It re-scores every completed round in order and refreshes the cached
/// standings, which is also the only route that fixes a finished season - the per-minute job only
/// visits seasons still flagged active, yet their leagues are still on the dashboard.
/// </summary>
public class RecalculateSeasonStatsCommandHandlerTests
{
    private const int SeasonId = 11;

    private static readonly DateTime SeasonStart = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly IRoundRepository _rounds = Substitute.For<IRoundRepository>();
    private readonly ILeagueRepository _leagues = Substitute.For<ILeagueRepository>();
    private readonly ILeagueStatsRepository _leagueStats = Substitute.For<ILeagueStatsRepository>();
    private readonly IBoostService _boostService = Substitute.For<IBoostService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IRoundResultsService _roundResultsService = Substitute.For<IRoundResultsService>();

    private readonly RecalculateSeasonStatsCommandHandler _handler;

    public RecalculateSeasonStatsCommandHandlerTests()
    {
        _handler = new RecalculateSeasonStatsCommandHandler(_rounds, _leagues, _leagueStats, _boostService, _roundResultsService, _mediator);
        _leagues.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        GivenRounds();
    }

    private static Round Round(int id, int roundNumber, RoundStatus status, DateTime startDateUtc) =>
        new(id: id, seasonId: SeasonId, roundNumber: roundNumber, displayName: $"Gameweek {roundNumber}",
            startDateUtc: startDateUtc, deadlineUtc: startDateUtc.AddMinutes(-30), status: status,
            apiRoundName: null, lastReminderSentUtc: null, matches: null);

    private void GivenRounds(params Round[] rounds) =>
        _rounds.GetAllForSeasonAsync(SeasonId, Arg.Any<CancellationToken>())
            .Returns(rounds.ToDictionary(r => r.Id));

    private Task HandleAsync() => _handler.Handle(new RecalculateSeasonStatsCommand(SeasonId), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldStillRefreshTheStandings_WhenNoRoundIsComplete()
    {
        // The dashboard ranks are cached, so they need rebuilding even if there is nothing to
        // re-score.
        GivenRounds(Round(1, 1, RoundStatus.Published, SeasonStart));

        await HandleAsync();

        await _roundResultsService.DidNotReceiveWithAnyArgs().RecalculateAsync(default!, CancellationToken.None);
        await _leagueStats.Received(1).RefreshSeasonAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRescoreOnlyCompletedRounds()
    {
        GivenRounds(
            Round(1, 1, RoundStatus.Completed, SeasonStart),
            Round(2, 2, RoundStatus.InProgress, SeasonStart.AddDays(7)),
            Round(3, 3, RoundStatus.Draft, SeasonStart.AddDays(14)));

        await HandleAsync();

        await _roundResultsService.Received(1).RecalculateAsync(Arg.Is<Round>(round => round.Id == 1), Arg.Any<CancellationToken>());
        await _roundResultsService.DidNotReceive().RecalculateAsync(Arg.Is<Round>(round => round.Id == 2), Arg.Any<CancellationToken>());
        await _roundResultsService.DidNotReceive().RecalculateAsync(Arg.Is<Round>(round => round.Id == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRescoreRoundsInChronologicalOrder()
    {
        // Later rounds build on earlier ones, so replaying them out of order would produce the
        // wrong running totals.
        GivenRounds(
            Round(3, 3, RoundStatus.Completed, SeasonStart.AddDays(14)),
            Round(1, 1, RoundStatus.Completed, SeasonStart),
            Round(2, 2, RoundStatus.Completed, SeasonStart.AddDays(7)));
        var order = new List<int>();
        await _roundResultsService.RecalculateAsync(Arg.Do<Round>(round => order.Add(round.Id)), Arg.Any<CancellationToken>());
        order.Clear();

        await HandleAsync();

        order.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldRebuildEachRoundThenApplyItsBoosts()
    {
        GivenRounds(Round(1, 1, RoundStatus.Completed, SeasonStart));

        await HandleAsync();

        Received.InOrder(() =>
        {
            _roundResultsService.RecalculateAsync(Arg.Is<Round>(round => round.Id == 1), Arg.Any<CancellationToken>());
            _leagues.UpdateLeagueRoundResultsAsync(1, Arg.Any<CancellationToken>());
            _boostService.ApplyRoundBoostsAsync(1, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ShouldReprocessPrizesForEveryLeagueInTheSeason()
    {
        GivenRounds(Round(1, 1, RoundStatus.Completed, SeasonStart));
        _leagues.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([7, 8]);

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<ProcessPrizesCommand>(c => c.RoundId == 1 && c.LeagueId == 7), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<ProcessPrizesCommand>(c => c.RoundId == 1 && c.LeagueId == 8), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefreshTheStandingsOnceAtTheEnd()
    {
        // Once, after everything - not per round, which would be wasted work and could leave the
        // cache describing a mid-rebuild state.
        GivenRounds(
            Round(1, 1, RoundStatus.Completed, SeasonStart),
            Round(2, 2, RoundStatus.Completed, SeasonStart.AddDays(7)));

        await HandleAsync();

        await _leagueStats.Received(1).RefreshSeasonAsync(SeasonId, Arg.Any<CancellationToken>());
    }
}
