using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Web.Client.Services.Leagues;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Services.Dashboard;

public class DashboardStateService : IDashboardStateService
{
    public List<MyLeagueDto> MyLeagues { get; private set; } = new();
    public List<AvailableLeagueDto> AvailableLeagues { get; private set; } = new();
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? AvailableLeaguesErrorMessage { get; private set; }

    public event Action? OnStateChange;

    private readonly HttpClient _http;
    private readonly ILeagueService _leagueService;

    public DashboardStateService(HttpClient http, ILeagueService leagueService)
    {
        _http = http;
        _leagueService = leagueService;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var myLeaguesTask = _http.GetFromJsonAsync<List<MyLeagueDto>>("api/dashboard/my-leagues");
            var availableLeaguesTask = _http.GetFromJsonAsync<List<AvailableLeagueDto>>("api/dashboard/available-leagues");

            await Task.WhenAll(myLeaguesTask, availableLeaguesTask);

            MyLeagues = await myLeaguesTask ?? new List<MyLeagueDto>();
            AvailableLeagues = await availableLeaguesTask ?? new List<AvailableLeagueDto>();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load dashboard data. Please try again later.";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task JoinPublicLeagueAsync(int leagueId)
    {
        AvailableLeaguesErrorMessage = null;
      
        var (success, errorMessage) = await _leagueService.JoinPublicLeagueAsync(leagueId);

        if (success)
        {
            await InitializeAsync();
        }
        else
        {
            AvailableLeaguesErrorMessage = errorMessage;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}