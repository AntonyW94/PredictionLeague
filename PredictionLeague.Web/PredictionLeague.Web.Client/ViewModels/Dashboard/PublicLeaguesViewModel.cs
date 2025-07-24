using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Web.Client.Services.Leagues;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.ViewModels.Dashboard;

public class PublicLeaguesViewModel
{
    public List<PublicLeagueDto> PublicLeagues { get; private set; } = new();
    public bool IsLoading { get; private set; } = true;
    public string? JoinLeagueError { get; private set; }

    private readonly HttpClient _http;
    private readonly ILeagueService _leagueService;

    public PublicLeaguesViewModel(HttpClient http, ILeagueService leagueService)
    {
        _http = http;
        _leagueService = leagueService;
    }

    public async Task LoadPublicLeaguesAsync()
    {
        IsLoading = true;
        try
        {
            var leagues = await _http.GetFromJsonAsync<List<PublicLeagueDto>>("api/dashboard/public-leagues");
            if (leagues != null)
                PublicLeagues = leagues;
        }
        catch
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task JoinLeagueAsync(int leagueId)
    {
        JoinLeagueError = null;
      
        var (success, errorMessage) = await _leagueService.JoinPublicLeagueAsync(leagueId); // Change this
        
        if (success)
            await LoadPublicLeaguesAsync();
        else
            JoinLeagueError = errorMessage;
    }
}