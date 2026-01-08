using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Leaderboards;
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

    public async Task<List<MyLeagueDto>> GetMyLeaguesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<MyLeagueDto>>("api/dashboard/my-leagues") ?? new List<MyLeagueDto>();
    }

    public async Task<List<AvailableLeagueDto>> GetAvailableLeaguesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<AvailableLeagueDto>>("api/dashboard/available-leagues") ?? new List<AvailableLeagueDto>();
    }

    public async Task<List<LeagueLeaderboardDto>> GetLeaderboardsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LeagueLeaderboardDto>>("api/dashboard/leaderboards") ?? new List<LeagueLeaderboardDto>();
    }

    public async Task<List<UpcomingRoundDto>> GetUpcomingRoundsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UpcomingRoundDto>>("api/dashboard/upcoming-rounds") ?? new List<UpcomingRoundDto>();
    }

    public async Task<List<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId)
    {
        return await _httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/overall") ?? new List<LeaderboardEntryDto>();
    }

    public async Task<ExactScoresLeaderboardDto> GetExactScoresLeaderboardAsync(int leagueId)
    {
        return await _httpClient.GetFromJsonAsync<ExactScoresLeaderboardDto>($"api/leagues/{leagueId}/leaderboard/exact-scores") ?? new ExactScoresLeaderboardDto();
    }

    public async Task<List<MonthDto>> GetMonthsForLeagueAsync(int leagueId)
    {
        return await _httpClient.GetFromJsonAsync<List<MonthDto>>($"api/leagues/{leagueId}/months") ?? new List<MonthDto>();
    }

    public async Task<List<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month)
    {
        return await _httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/monthly/{month}") ?? new List<LeaderboardEntryDto>();
    }

    public async Task<WinningsDto> GetWinningsAsync(int leagueId)
    {
        return await _httpClient.GetFromJsonAsync<WinningsDto>($"api/leagues/{leagueId}/winnings") ?? new WinningsDto();
    }

    public async Task<bool> CheckForAvailablePrivateLeaguesAsync()
    {
        return await _httpClient.GetFromJsonAsync<bool>("api/Dashboard/private-leagues-available");
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

    public async Task<List<LeagueRequestDto>> GetPendingRequestsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LeagueRequestDto>>("api/dashboard/pending-requests") ?? new List<LeagueRequestDto>();
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelJoinRequestAsync(int leagueId)
    {
        var response = await _httpClient.DeleteAsync($"api/leagues/{leagueId}/join-request");
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "Could not cancel request.";
            return (false, errorMessage);
        }
        catch
        {
            return (false, "An unexpected error occurred.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DismissAlertAsync(int leagueId)
    {
        var response = await _httpClient.PutAsync($"api/leagues/{leagueId}/dismiss-alert", null);
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "Could not dismiss notification.";
            return (false, errorMessage);
        }
        catch
        {
            return (false, "An unexpected error occurred.");
        }
    }
}