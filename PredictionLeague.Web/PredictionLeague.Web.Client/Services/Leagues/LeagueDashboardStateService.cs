using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Services.Leagues;

public class LeagueDashboardStateService
{
    public event Action? OnStateChange;

    public string? LeagueName { get; private set; }
    public List<RoundDto> ViewableRounds { get; private set; } = new();
    public List<PredictionResultDto> CurrentRoundResults { get; private set; } = new();
    public List<MatchInRoundDto> CurrentRoundMatches { get; private set; } = new();
    
    public int? SelectedRoundId { get; set; }
    
    public bool IsLoadingDashboard { get; private set; }
    public bool IsLoadingRoundResults { get; private set; }
    public string? DashboardLoadError { get; private set; }
    public string? RoundResultsError { get; private set; }
    
    private readonly HttpClient _httpClient;

    public LeagueDashboardStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task LoadDashboardData(int leagueId)
    {
        IsLoadingDashboard = true;
        DashboardLoadError = null;
       
        NotifyStateChanged();

        try
        {
            var data = await _httpClient.GetFromJsonAsync<LeagueDashboardDto>($"api/leagues/{leagueId}/dashboard-data");
            if (data != null)
            {
                LeagueName = data.LeagueName;
                ViewableRounds = data.ViewableRounds;

                if (ViewableRounds.Any())
                {
                    var defaultRound = ViewableRounds.OrderBy(r => r.StartDate).FirstOrDefault(r => r.Status == RoundStatus.Published);
                    if (defaultRound != null)
                    {
                        SelectedRoundId = defaultRound.Id;
                        await LoadRoundResults(leagueId, SelectedRoundId.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DashboardLoadError = "Could not load dashboard data.";
        }
        finally
        {
            IsLoadingDashboard = false;
            NotifyStateChanged();
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
            var resultsTask = _httpClient.GetFromJsonAsync<List<PredictionResultDto>>($"api/leagues/{leagueId}/rounds/{roundId}/results");
            var matchesTask =  _httpClient.GetFromJsonAsync<List<MatchInRoundDto>>($"api/rounds/{roundId}/matches-data");

            await Task.WhenAll(resultsTask, matchesTask);

            CurrentRoundResults = resultsTask.Result ?? new List<PredictionResultDto>();
            CurrentRoundMatches = matchesTask.Result ?? new List<MatchInRoundDto>();
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

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}