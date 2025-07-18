
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Web.Client.Authentication;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.ViewModels;

public class DashboardViewModel
{
    public DashboardDto? DashboardData { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public int CurrentRoundIndex { get; private set; }

    private readonly HttpClient _http;
    private readonly IAuthenticationService _authenticationService;

    public DashboardViewModel(HttpClient http, IAuthenticationService authenticationService)
    {
        _http = http;
        _authenticationService = authenticationService;
    }

    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        
        try
        {
            DashboardData = await _http.GetFromJsonAsync<DashboardDto>("api/dashboard/dashboard-data");
        }
        catch (Exception)
        {
            // Handle error, e.g., set an error message property
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ShowPreviousRound()
    {
        if (CurrentRoundIndex > 0)
        {
            CurrentRoundIndex--;
        }
    }

    public void ShowNextRound()
    {
        if (DashboardData != null && CurrentRoundIndex < DashboardData.UpcomingRounds.Count - 1)
        {
            CurrentRoundIndex++;
        }
    }

    public async Task<bool> JoinLeagueAsync(int leagueId)
    {
        var result = await _authenticationService.JoinPublicLeagueAsync(leagueId);
        if (result)
            await LoadDashboardDataAsync();
        
        return result;
    }
}