using FluentAssertions;
using NSubstitute;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.Services.Dashboard;
using ThePredictions.Web.Client.Services.Leagues;
using ThePredictions.Web.Client.Services.Onboarding;
using ThePredictions.Web.Client.Services.SeasonPasses;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Dashboard;

public class DashboardStateServiceLiveRefreshTests
{
    private readonly ILeagueService _leagueService = Substitute.For<ILeagueService>();
    private readonly DashboardStateService _state;

    public DashboardStateServiceLiveRefreshTests()
    {
        _state = new DashboardStateService(
            _leagueService,
            Substitute.For<ISeasonPassService>(),
            Substitute.For<IOnboardingService>());
    }

    private static readonly DateTime MatchTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ActiveRoundDto Round(int id, MatchStatus matchStatus) =>
        new(id, "Season", 1, null, false, MatchTime, MatchTime, false, RoundStatus.InProgress, new[] { Match(matchStatus) }, null);

    private static ActiveRoundMatchDto Match(MatchStatus status) =>
        new(null, null, null, null, null, status, null, null, MatchTime, 1, true, null, null, false, 0, 0, 0, null);

    private static LeagueLeaderboardDto Board(int totalPoints, bool roundInProgress) =>
        new()
        {
            LeagueId = 1,
            LeagueName = "League",
            SeasonName = "Season",
            Entries = new List<LeaderboardEntryDto>
            {
                new() { UserId = "u1", PlayerName = "u1", Rank = 1, TotalPoints = totalPoints, IsRoundInProgress = roundInProgress }
            }
        };

    [Fact]
    public async Task IsAnyRoundLive_ShouldBeTrue_WhenAnActiveRoundHasAMatchInProgress()
    {
        _leagueService.GetActiveRoundsAsync().Returns(new List<ActiveRoundDto> { Round(1, MatchStatus.InProgress) });

        await _state.LoadActiveRoundsAsync();

        _state.IsAnyRoundLive.Should().BeTrue();
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled)]
    [InlineData(MatchStatus.Completed)]
    [InlineData(MatchStatus.Postponed)]
    public async Task IsAnyRoundLive_ShouldBeFalse_WhenNoMatchIsInProgress_EvenIfRoundIsInProgress(MatchStatus matchStatus)
    {
        // The round itself is RoundStatus.InProgress (set by Round(...)), but no match is
        // actually being played, so we must not poll during the gap.
        _leagueService.GetActiveRoundsAsync().Returns(new List<ActiveRoundDto> { Round(1, matchStatus) });

        await _state.LoadActiveRoundsAsync();

        _state.IsAnyRoundLive.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldNotNotify_WhenDataUnchanged()
    {
        _leagueService.GetActiveRoundsAsync().Returns(new List<ActiveRoundDto> { Round(1, MatchStatus.InProgress) });
        _leagueService.GetLeaderboardsAsync().Returns(new List<LeagueLeaderboardDto> { Board(3, roundInProgress: true) });

        await _state.LoadActiveRoundsAsync();
        await _state.LoadLeaderboardsAsync();

        var notified = 0;
        _state.OnStateChange += () => notified++;

        await _state.RefreshLiveDataAsync();

        notified.Should().Be(0, "an unchanged poll must not re-render (no flicker)");
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldNotifyOnce_WhenStandingsChange()
    {
        _leagueService.GetActiveRoundsAsync().Returns(new List<ActiveRoundDto> { Round(1, MatchStatus.InProgress) });
        _leagueService.GetLeaderboardsAsync().Returns(
            new List<LeagueLeaderboardDto> { Board(3, roundInProgress: true) },
            new List<LeagueLeaderboardDto> { Board(6, roundInProgress: true) });

        await _state.LoadActiveRoundsAsync();
        await _state.LoadLeaderboardsAsync();

        var notified = 0;
        _state.OnStateChange += () => notified++;

        await _state.RefreshLiveDataAsync();

        notified.Should().Be(1);
        _state.Leaderboards.Single().Entries.Single().TotalPoints.Should().Be(6);
    }

    [Fact]
    public async Task RefreshLiveDataAsync_ShouldKeepLastKnownValues_WhenPollFails()
    {
        var baseline = new List<ActiveRoundDto> { Round(1, MatchStatus.InProgress) };
        _leagueService.GetActiveRoundsAsync().Returns(baseline);
        _leagueService.GetLeaderboardsAsync().Returns(new List<LeagueLeaderboardDto> { Board(3, roundInProgress: true) });

        await _state.LoadActiveRoundsAsync();
        await _state.LoadLeaderboardsAsync();

        var notified = 0;
        _state.OnStateChange += () => notified++;

        _leagueService.GetActiveRoundsAsync().Returns<List<ActiveRoundDto>>(_ => throw new HttpRequestException("down"));

        var act = async () => await _state.RefreshLiveDataAsync();

        await act.Should().NotThrowAsync();
        notified.Should().Be(0);
        _state.ActiveRounds.Should().ContainSingle().Which.Status.Should().Be(RoundStatus.InProgress);
    }
}
