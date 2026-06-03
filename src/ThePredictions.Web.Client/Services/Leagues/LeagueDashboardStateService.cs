using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using System.Net.Http.Json;

namespace ThePredictions.Web.Client.Services.Leagues;

public class LeagueDashboardStateService(HttpClient httpClient)
{
    public event Action? OnStateChange;

    public string? LeagueName { get; private set; }
    public int CompetitionType { get; private set; }
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
    public SeasonRecapDto? SeasonRecap { get; private set; }
    public LeagueRecordsDto? LeagueRecords { get; private set; }
    public PrizeBreakdownDto? PrizeBreakdown { get; private set; }

    public int? SelectedRoundId { get; set; }

    public bool IsLoadingDashboard { get; private set; }
    public bool IsLoadingRoundResults { get; private set; }
    public bool IsLoadingSeasonRecap { get; private set; }
    public bool IsLoadingLeagueRecords { get; private set; }

    public string? DashboardLoadError { get; private set; }
    public string? RoundResultsError { get; private set; }
    public string? SeasonRecapError { get; private set; }
    public string? LeagueRecordsError { get; private set; }

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

                if (EntryDeadlineUtc is { } deadline && DateTime.UtcNow < deadline && !IsFinished)
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