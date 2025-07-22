using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Web.Client.Authentication;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.ViewModels.Dashboard;

public class PublicLeaguesViewModel
{
    public List<PublicLeagueDto> PublicLeagues { get; private set; } = new();
    public bool IsLoading { get; private set; } = true;

    private readonly HttpClient _http;
    private readonly IAuthenticationService _authenticationService;

    public PublicLeaguesViewModel(HttpClient http, IAuthenticationService authenticationService)
    {
        _http = http;
        _authenticationService = authenticationService;
    }

    public async Task LoadPublicLeaguesAsync()
    {
        IsLoading = true;
        try
        {
            var leagues = await _http.GetFromJsonAsync<List<PublicLeagueDto>>("api/dashboard/public-leagues");
            if (leagues != null)
            {
                PublicLeagues = leagues;
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task JoinLeagueAsync(int leagueId)
    {
        var result = await _authenticationService.JoinPublicLeagueAsync(leagueId);
        if (result)
            await LoadPublicLeaguesAsync();
    }
}