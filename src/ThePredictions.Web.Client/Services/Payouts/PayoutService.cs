using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Web.Client.Services.Payouts;

public class PayoutService(HttpClient httpClient) : IPayoutService
{
    public async Task<MyPayoutDetailsDto?> GetMyPayoutDetailsAsync()
    {
        var response = await httpClient.GetAsync("api/account/payout-details");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<MyPayoutDetailsDto>();
    }

    public async Task<(bool Success, string? ErrorMessage)> SetMyPayoutDetailsAsync(SetPayoutDetailsRequest request)
    {
        var response = await httpClient.PutAsJsonAsync("api/account/payout-details", request);
        return await ToResultAsync(response, "An error occurred while saving your payout details.");
    }

    public async Task<bool> DeleteMyPayoutDetailsAsync()
    {
        var response = await httpClient.DeleteAsync("api/account/payout-details");
        return response.IsSuccessStatusCode;
    }

    public async Task<LeaguePayoutsDto?> GetLeaguePayoutsAsync(int leagueId)
    {
        var response = await httpClient.GetAsync($"api/leagues/{leagueId}/payouts");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<LeaguePayoutsDto>();
    }

    public async Task<(bool Success, string? ErrorMessage)> MarkPayoutPaidAsync(int leagueId, string winnerUserId)
    {
        var response = await httpClient.PostAsync($"api/leagues/{leagueId}/payouts/{winnerUserId}/mark-paid", null);
        return await ToResultAsync(response, "An error occurred while marking the payout as paid.");
    }

    private static async Task<(bool Success, string? ErrorMessage)> ToResultAsync(HttpResponseMessage response, string fallback)
    {
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var message = errorContent?["message"]?.ToString()
                          ?? errorContent?["errors"]?[0]?["ErrorMessage"]?.ToString()
                          ?? fallback;
            return (false, message);
        }
        catch
        {
            return (false, fallback);
        }
    }
}
