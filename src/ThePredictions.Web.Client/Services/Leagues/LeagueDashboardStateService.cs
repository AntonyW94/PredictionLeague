using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using System.Net.Http.Json;
using System.Text.Json;

namespace ThePredictions.Web.Client.Services.Leagues;

public class LeagueDashboardStateService(HttpClient httpClient)
{
    public event Action? OnStateChange;

    /// <summary>
    /// Raised after a live poll detects changed data. Tiles that own their own
    /// data (the monthly / exact-scores / stage leaderboards) subscribe to this
    /// to re-fetch their current selection, separate from the structural
    /// <see cref="OnStateChange"/> that fires during loads and round switches.
    /// </summary>
    public event Action? OnLiveDataChanged;

    public string? LeagueName { get; private set; }
    public CompetitionType CompetitionType { get; private set; }
    public DateTime? SeasonStartDateUtc { get; private set; }
    public DateTime? EntryDeadlineUtc { get; private set; }
    public int MemberCount { get; private set; }
    public decimal TotalPrizeFund { get; private set; }
    public bool IsFinished { get; private set; }
    public bool IsFree { get; private set; }
    public List<LeagueDashboardMemberDto> Members { get; private set; } = [];
    public List<RoundDto> ViewableRounds { get; private set; } = [];
    public List<PredictionResultDto> CurrentRoundResults { get; private set; } = [];
    public List<MatchInRoundDto> CurrentRoundMatches { get; private set; } = [];
    public List<LeaderboardEntryDto> OverallLeaderboard { get; private set; } = [];
    public SeasonRecapDto? SeasonRecap { get; private set; }
    public LeagueRecordsDto? LeagueRecords { get; private set; }
    public PrizeBreakdownDto? PrizeBreakdown { get; private set; }

    public int? SelectedRoundId { get; set; }

    public bool IsLoadingDashboard { get; private set; }
    public bool IsLoadingRoundResults { get; private set; }
    public bool IsLoadingOverallLeaderboard { get; private set; }
    public bool IsLoadingSeasonRecap { get; private set; }
    public bool IsLoadingLeagueRecords { get; private set; }

    public string? DashboardLoadError { get; private set; }
    public string? RoundResultsError { get; private set; }
    public string? OverallLeaderboardError { get; private set; }
    public string? SeasonRecapError { get; private set; }
    public string? LeagueRecordsError { get; private set; }

    /// <summary>
    /// True when the currently-selected round is in progress, or any of its
    /// matches are in progress. Drives whether live-score polling should run.
    /// </summary>
    public bool IsSelectedRoundLive
    {
        get
        {
            // Any match currently playing is always live.
            if (CurrentRoundMatches.Any(m => m.Status == MatchStatus.InProgress))
                return true;

            var selectedRound = ViewableRounds.FirstOrDefault(r => r.Id == SelectedRoundId);
            if (selectedRound is not { Status: RoundStatus.InProgress })
                return false;

            // The round is in progress: keep polling before kick-off and between
            // matches (any match still to play). Once every match has finished we
            // treat it as no longer live so polling can stop, even though the
            // round's own status is only re-fetched on a full page load.
            return CurrentRoundMatches.Count == 0
                   || CurrentRoundMatches.Any(m => m.Status == MatchStatus.Scheduled);
        }
    }

    public async Task LoadDashboardData(int leagueId)
    {
        IsLoadingDashboard = true;
        DashboardLoadError = null;

        NotifyStateChanged();

        try
        {
            var data = await httpClient.GetFromJsonAsync<LeagueDashboardDto>($"api/leagues/{leagueId}/dashboard-data");
            if (data != null)
            {
                LeagueName = data.LeagueName;
                CompetitionType = data.CompetitionType;
                SeasonStartDateUtc = data.SeasonStartDateUtc;
                EntryDeadlineUtc = data.EntryDeadlineUtc;
                MemberCount = data.MemberCount;
                TotalPrizeFund = data.TotalPrizeFund;
                IsFinished = data.IsFinished;
                IsFree = data.IsFree;
                Members = data.Members;
                ViewableRounds = data.ViewableRounds;

                if (!IsFinished)
                    await LoadPrizeBreakdown(leagueId);

                if (ViewableRounds.Any())
                {
                    var defaultRound =
                        ViewableRounds.OrderBy(r => r.RoundNumber).FirstOrDefault(r => r.Status == RoundStatus.InProgress)
                        ?? ViewableRounds.OrderBy(r => r.RoundNumber).FirstOrDefault(r => r.Status == RoundStatus.Published)
                        ?? ViewableRounds.OrderByDescending(r => r.RoundNumber).FirstOrDefault(r => r.Status == RoundStatus.Completed);

                    if (defaultRound != null)
                    {
                        SelectedRoundId = defaultRound.Id;
                        await LoadRoundResults(leagueId, SelectedRoundId.Value);
                    }

                    await LoadOverallLeaderboard(leagueId);
                }

                if (IsFinished)
                {
                    await Task.WhenAll(
                        LoadSeasonRecap(leagueId),
                        LoadLeagueRecords(leagueId));
                }
            }
        }
        catch (Exception)
        {
            DashboardLoadError = "Could not load dashboard data.";
        }
        finally
        {
            IsLoadingDashboard = false;
            NotifyStateChanged();
        }
    }

    private async Task LoadPrizeBreakdown(int leagueId)
    {
        try
        {
            PrizeBreakdown = await httpClient.GetFromJsonAsync<PrizeBreakdownDto>($"api/leagues/{leagueId}/prize-breakdown");
        }
        catch
        {
            PrizeBreakdown = null;
        }
    }

    public async Task LoadRoundResults(int leagueId, int roundId)
    {
        IsLoadingRoundResults = true;
        SelectedRoundId = roundId;
        RoundResultsError = null;

        NotifyStateChanged();

        try
        {
            var resultsTask = httpClient.GetFromJsonAsync<List<PredictionResultDto>>($"api/leagues/{leagueId}/rounds/{roundId}/results");
            var matchesTask = httpClient.GetFromJsonAsync<List<MatchInRoundDto>>($"api/rounds/{roundId}/matches-data");

            await Task.WhenAll(resultsTask, matchesTask);

            CurrentRoundResults = resultsTask.Result ?? [];
            CurrentRoundMatches = matchesTask.Result ?? [];
        }
        catch
        {
            RoundResultsError = "Could not load results for the selected round.";
        }
        finally
        {
            IsLoadingRoundResults = false;
            NotifyStateChanged();
        }
    }

    public async Task LoadOverallLeaderboard(int leagueId)
    {
        IsLoadingOverallLeaderboard = true;
        OverallLeaderboardError = null;

        NotifyStateChanged();

        try
        {
            OverallLeaderboard = await httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/overall") ?? [];
        }
        catch
        {
            OverallLeaderboardError = "Could not load the leaderboard. Please try again later.";
        }
        finally
        {
            IsLoadingOverallLeaderboard = false;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Silently re-fetches the live data for the selected round (match scores and
    /// statuses, per-round points and the overall leaderboard) and only raises
    /// <see cref="OnStateChange"/> when something actually changed, so the UI does
    /// not flicker on unchanged polls. A failed poll keeps the last-known values.
    /// </summary>
    public async Task RefreshLiveDataAsync(int leagueId, CancellationToken cancellationToken = default)
    {
        if (SelectedRoundId is not { } roundId)
            return;

        try
        {
            var resultsTask = httpClient.GetFromJsonAsync<List<PredictionResultDto>>($"api/leagues/{leagueId}/rounds/{roundId}/results", cancellationToken);
            var matchesTask = httpClient.GetFromJsonAsync<List<MatchInRoundDto>>($"api/rounds/{roundId}/matches-data", cancellationToken);
            var leaderboardTask = httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/overall", cancellationToken);

            await Task.WhenAll(resultsTask, matchesTask, leaderboardTask);

            var newResults = resultsTask.Result ?? [];
            var newMatches = matchesTask.Result ?? [];
            var newLeaderboard = leaderboardTask.Result ?? [];

            var changed =
                HasChanged(CurrentRoundResults, newResults)
                | HasChanged(CurrentRoundMatches, newMatches)
                | HasChanged(OverallLeaderboard, newLeaderboard);

            if (!changed)
                return;

            CurrentRoundResults = newResults;
            CurrentRoundMatches = newMatches;
            OverallLeaderboard = newLeaderboard;

            NotifyStateChanged();
            OnLiveDataChanged?.Invoke();
        }
        catch
        {
            // Keep the last-known values on a failed poll; the page must not crash.
        }
    }

    private static bool HasChanged<T>(List<T> current, List<T> updated) =>
        JsonSerializer.Serialize(current) != JsonSerializer.Serialize(updated);

    public async Task LoadSeasonRecap(int leagueId)
    {
        IsLoadingSeasonRecap = true;
        SeasonRecapError = null;
        NotifyStateChanged();

        try
        {
            SeasonRecap = await httpClient.GetFromJsonAsync<SeasonRecapDto>($"api/leagues/{leagueId}/season-recap");
        }
        catch
        {
            SeasonRecapError = "Could not load your season summary.";
        }
        finally
        {
            IsLoadingSeasonRecap = false;
            NotifyStateChanged();
        }
    }

    public async Task LoadLeagueRecords(int leagueId)
    {
        IsLoadingLeagueRecords = true;
        LeagueRecordsError = null;
        NotifyStateChanged();

        try
        {
            LeagueRecords = await httpClient.GetFromJsonAsync<LeagueRecordsDto>($"api/leagues/{leagueId}/records");
        }
        catch
        {
            LeagueRecordsError = "Could not load league records.";
        }
        finally
        {
            IsLoadingLeagueRecords = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}