using System.Net;
using FluentAssertions;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.Services.Leagues;
using ThePredictions.Web.Client.Tests.Unit.TestDoubles;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Leagues;

public class LeagueDashboardStateServiceLiveRefreshTests
{
    private const int LeagueId = 1;
    private const int RoundId = 10;

    private readonly StubHttpMessageHandler _handler = new();
    private readonly LeagueDashboardStateService _service;

    public LeagueDashboardStateServiceLiveRefreshTests()
    {
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://localhost/") };
        _service = new LeagueDashboardStateService(httpClient);
    }

    private static PredictionResultDto Result(string userId, int points, long rank) =>
        new() { UserId = userId, PlayerName = userId, HasPredicted = true, TotalPoints = points, Rank = rank };

    private static MatchInRoundDto Match(int id, int? home, int? away, MatchStatus status) =>
        new(id, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, 1, "Home", "HOM", "H", null, 2, "Away", "AWA", "A", null, home, away, status);

    private static LeaderboardEntryDto Entry(string userId, int points, long rank) =>
        new() { UserId = userId, PlayerName = userId, TotalPoints = points, Rank = rank, IsRoundInProgress = true };

    // Establishes a baseline: a selected round with one match/result and one leaderboard entry.
    // Responses are enqueued in the exact order the service requests them (FIFO handler).
    private async Task SeedBaselineAsync()
    {
        _handler
            .EnqueueJson(HttpStatusCode.OK, new List<PredictionResultDto> { Result("u1", 3, 1) })   // results
            .EnqueueJson(HttpStatusCode.OK, new List<MatchInRoundDto> { Match(1, 1, 0, MatchStatus.InProgress) }); // matches
        await _service.LoadRoundResults(LeagueId, RoundId);

        _handler.EnqueueJson(HttpStatusCode.OK, new List<LeaderboardEntryDto> { Entry("u1", 3, 1) }); // leaderboard
        await _service.LoadOverallLeaderboard(LeagueId);

        _handler.Requests.Clear();
    }

    [Fact]
    public async Task IsSelectedRoundLive_ShouldBeTrue_WhenAMatchIsInProgress()
    {
        _handler
            .EnqueueJson(HttpStatusCode.OK, new List<PredictionResultDto> { Result("u1", 3, 1) })
            .EnqueueJson(HttpStatusCode.OK, new List<MatchInRoundDto> { Match(1, 1, 0, MatchStatus.InProgress) });

        await _service.LoadRoundResults(LeagueId, RoundId);

        _service.IsSelectedRoundLive.Should().BeTrue();
    }

    [Fact]
    public async Task IsSelectedRoundLive_ShouldBeFalse_WhenNoMatchIsLiveAndRoundStatusUnknown()
    {
        _handler
            .EnqueueJson(HttpStatusCode.OK, new List<PredictionResultDto> { Result("u1", 0, 1) })
            .EnqueueJson(HttpStatusCode.OK, new List<MatchInRoundDto> { Match(1, null, null, MatchStatus.Completed) });

        await _service.LoadRoundResults(LeagueId, RoundId);

        _service.IsSelectedRoundLive.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldReturnWithoutFetching_WhenNoRoundSelected()
    {
        var notified = 0;
        _service.OnStateChange += () => notified++;

        await _service.RefreshLiveDataAsync(LeagueId, CancellationToken.None);

        _handler.SendCount.Should().Be(0);
        notified.Should().Be(0);
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldNotNotify_WhenDataUnchanged()
    {
        await SeedBaselineAsync();

        var notified = 0;
        _service.OnStateChange += () => notified++;

        // Same three payloads as the baseline: nothing has changed.
        _handler
            .EnqueueJson(HttpStatusCode.OK, new List<PredictionResultDto> { Result("u1", 3, 1) })
            .EnqueueJson(HttpStatusCode.OK, new List<MatchInRoundDto> { Match(1, 1, 0, MatchStatus.InProgress) })
            .EnqueueJson(HttpStatusCode.OK, new List<LeaderboardEntryDto> { Entry("u1", 3, 1) });

        await _service.RefreshLiveDataAsync(LeagueId, CancellationToken.None);

        notified.Should().Be(0, "an unchanged poll must not re-render (no flicker)");
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldNotifyOnce_WhenScoresChange()
    {
        await SeedBaselineAsync();

        var notified = 0;
        _service.OnStateChange += () => notified++;

        // The match score has moved on and points have changed.
        _handler
            .EnqueueJson(HttpStatusCode.OK, new List<PredictionResultDto> { Result("u1", 6, 1) })
            .EnqueueJson(HttpStatusCode.OK, new List<MatchInRoundDto> { Match(1, 2, 0, MatchStatus.InProgress) })
            .EnqueueJson(HttpStatusCode.OK, new List<LeaderboardEntryDto> { Entry("u1", 6, 1) });

        await _service.RefreshLiveDataAsync(LeagueId, CancellationToken.None);

        notified.Should().Be(1);
        _service.CurrentRoundMatches.Single().ActualHomeTeamScore.Should().Be(2);
        _service.OverallLeaderboard.Single().TotalPoints.Should().Be(6);
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldKeepLastKnownValues_WhenPollFails()
    {
        await SeedBaselineAsync();

        var notified = 0;
        _service.OnStateChange += () => notified++;

        // The API is unavailable for this poll.
        _handler.FallbackStatus = HttpStatusCode.InternalServerError;
        _handler
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError);

        var act = async () => await _service.RefreshLiveDataAsync(LeagueId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        notified.Should().Be(0);
        _service.CurrentRoundMatches.Single().ActualHomeTeamScore.Should().Be(1, "the last-known score is preserved");
        _service.OverallLeaderboard.Single().TotalPoints.Should().Be(3);
    }
}
