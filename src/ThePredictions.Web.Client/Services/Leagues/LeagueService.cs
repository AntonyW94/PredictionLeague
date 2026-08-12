using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Nodes;

namespace ThePredictions.Web.Client.Services.Leagues;

[ExcludeFromCodeCoverage(Justification = "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply.")]
public class LeagueService(HttpClient httpClient) : ILeagueService
{
    public async Task<List<MyLeagueDto>> GetMyLeaguesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<MyLeagueDto>>("api/dashboard/my-leagues") ?? [];
    }

    public async Task<List<AvailableLeagueDto>> GetAvailableLeaguesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<AvailableLeagueDto>>("api/dashboard/available-leagues") ?? [];
    }

    public async Task<List<LeagueLeaderboardDto>> GetLeaderboardsAsync()
    {
        return await httpClient.GetFromJsonAsync<List<LeagueLeaderboardDto>>("api/dashboard/leaderboards") ?? [];
    }

    public async Task<List<ActiveRoundDto>> GetActiveRoundsAsync()
    {
        return await httpClient.GetFromJsonAsync<List<ActiveRoundDto>>("api/dashboard/active-rounds") ?? [];
    }

    public async Task<byte[]?> GetShareCardAsync(int roundId, string theme)
    {
        var response = await httpClient.GetAsync($"api/rounds/{roundId}/share-card?theme={Uri.EscapeDataString(theme)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<List<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId)
    {
        return await httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/overall") ?? [];
    }

    public async Task<ExactScoresLeaderboardDto> GetExactScoresLeaderboardAsync(int leagueId)
    {
        return await httpClient.GetFromJsonAsync<ExactScoresLeaderboardDto>($"api/leagues/{leagueId}/leaderboard/exact-scores") ?? new ExactScoresLeaderboardDto();
    }

    public async Task<List<MonthDto>> GetMonthsForLeagueAsync(int leagueId)
    {
        return await httpClient.GetFromJsonAsync<List<MonthDto>>($"api/leagues/{leagueId}/months") ?? [];
    }

    public async Task<List<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month)
    {
        return await httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/monthly/{month}") ?? [];
    }

    public async Task<List<StageDto>> GetStagesForLeagueAsync(int leagueId)
    {
        return await httpClient.GetFromJsonAsync<List<StageDto>>($"api/leagues/{leagueId}/stages") ?? [];
    }

    public async Task<List<LeaderboardEntryDto>> GetStageLeaderboardAsync(int leagueId, TournamentStageGroup stage)
    {
        return await httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"api/leagues/{leagueId}/leaderboard/stage/{stage}") ?? [];
    }

    public async Task<WinningsDto> GetWinningsAsync(int leagueId)
    {
        return await httpClient.GetFromJsonAsync<WinningsDto>($"api/leagues/{leagueId}/winnings") ?? new WinningsDto();
    }

    public async Task<PrizeBreakdownDto?> GetPrizeBreakdownAsync(int leagueId)
    {
        var response = await httpClient.GetAsync($"api/leagues/{leagueId}/prize-breakdown");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PrizeBreakdownDto>();
    }

    public async Task<LeaguePaymentInfoDto?> GetPaymentInfoAsync(int leagueId)
    {
        var response = await httpClient.GetAsync($"api/leagues/{leagueId}/payment-info");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<LeaguePaymentInfoDto>();
    }

    public async Task<List<BoostUsageSummaryDto>> GetBoostUsageSummaryAsync(int leagueId)
    {
        return await httpClient.GetFromJsonAsync<List<BoostUsageSummaryDto>>($"api/leagues/{leagueId}/boost-usage") ?? [];
    }

    public async Task<bool> CheckForAvailablePrivateLeaguesAsync()
    {
        return await httpClient.GetFromJsonAsync<bool>("api/Dashboard/private-leagues-available");
    }

    public async Task<(bool Success, string? ErrorMessage, bool NeedsSeasonPass)> JoinPublicLeagueAsync(int leagueId)
    {
        var response = await httpClient.PostAsync($"api/leagues/{leagueId}/join", null);
        if (response.IsSuccessStatusCode)
            return (true, null, false);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred while trying to join the league.";
            return (false, errorMessage, NeedsSeasonPass(response));
        }
        catch
        {
            return (false, "An unexpected error occurred.", NeedsSeasonPass(response));
        }
    }

    /// <summary>
    /// Whether the join was refused for want of a season pass, rather than for any other reason.
    /// </summary>
    /// <remarks>
    /// The API already answers this distinctly - 402 Payment Required, carrying the season id - because the gate is a
    /// domain rule with its own exception type. Telling it apart from an ordinary refusal is what lets the caller offer
    /// the pass rather than an error message.
    /// </remarks>
    private static bool NeedsSeasonPass(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.PaymentRequired;

    public async Task<(PrizePreviewDto? Preview, string? ErrorMessage)> GetJoinPreviewByIdAsync(int leagueId)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/leagues/{leagueId}/prize-preview");
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<PrizePreviewDto>(), null);

            return (null, "We couldn't load this league. Please try again.");
        }
        catch (HttpRequestException)
        {
            return (null, "We couldn't reach the server. Please try again.");
        }
    }

    public async Task<(PrizePreviewDto? Preview, string? ErrorMessage)> GetJoinPreviewAsync(string entryCode)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/leagues/join-preview?entryCode={Uri.EscapeDataString(entryCode)}");
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<PrizePreviewDto>(), null);

            return (null, "We couldn't find a league for that code. Check it and try again.");
        }
        catch (HttpRequestException)
        {
            return (null, "We couldn't reach the server. Please try again.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage, int? LeagueId, bool NeedsSeasonPass)> JoinPrivateLeagueAsync(string entryCode)
    {
        var request = new JoinLeagueRequest { EntryCode = entryCode };

        var response = await httpClient.PostAsJsonAsync("api/leagues/join", request);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JoinLeagueResultDto>();
            return (true, null, result?.LeagueId, false);
        }

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred.";
            return (false, errorMessage, null, NeedsSeasonPass(response));
        }
        catch
        {
            return (false, "An unexpected error occurred.", null, NeedsSeasonPass(response));
        }
    }

    public async Task<List<LeagueRequestDto>> GetPendingRequestsAsync()
    {
        return await httpClient.GetFromJsonAsync<List<LeagueRequestDto>>("api/dashboard/pending-requests") ?? [];
    }

    public async Task<PendingMembersResultDto> GetPendingMembersForAdminAsync()
    {
        return await httpClient.GetFromJsonAsync<PendingMembersResultDto>("api/dashboard/pending-members")
               ?? new PendingMembersResultDto();
    }

    public async Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus newStatus)
    {
        await httpClient.PostAsJsonAsync($"api/leagues/{leagueId}/members/{userId}/status", newStatus);
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelJoinRequestAsync(int leagueId)
    {
        var response = await httpClient.DeleteAsync($"api/leagues/{leagueId}/join-request");
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
        var response = await httpClient.PutAsync($"api/leagues/{leagueId}/dismiss-alert", null);
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

    public async Task<(bool Success, string? ErrorMessage)> SetLeagueArchivedAsync(int leagueId, bool isArchived)
    {
        var endpoint = isArchived ? "archive" : "unarchive";
        var response = await httpClient.PutAsync($"api/leagues/{leagueId}/{endpoint}", null);
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "Could not update league.";
            return (false, errorMessage);
        }
        catch
        {
            return (false, "An unexpected error occurred.");
        }
    }
}
