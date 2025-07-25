using PredictionLeague.Contracts.Leagues;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PredictionLeague.Web.Client.Services.Leagues;

public class LeagueService : ILeagueService
{
    private readonly HttpClient _httpClient;

    public LeagueService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(bool Success, string? ErrorMessage)> JoinPublicLeagueAsync(int leagueId)
    {
        var response = await _httpClient.PostAsync($"api/leagues/{leagueId}/join", null);
        if (response.IsSuccessStatusCode)
            return (true, null);
        
        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred while trying to join the league.";
            return (false, errorMessage);
        }
        catch
        {
            return (false, "An unexpected error occurred.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> JoinPrivateLeagueAsync(string entryCode)
    {
        var request = new JoinLeagueRequest { EntryCode = entryCode };
       
        var response = await _httpClient.PostAsJsonAsync("api/leagues/join", request);
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred.";
            return (false, errorMessage);
        }
        catch
        {
            return (false, "An unexpected error occurred.");
        }
    }
}