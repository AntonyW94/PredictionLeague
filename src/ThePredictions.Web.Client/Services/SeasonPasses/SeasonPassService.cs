using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Web.Client.Services.SeasonPasses;

[ExcludeFromCodeCoverage(Justification = "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply.")]
public class SeasonPassService(HttpClient httpClient) : ISeasonPassService
{
    public async Task<List<MySeasonPassDto>> GetMyPassesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<MySeasonPassDto>>("api/season-passes/mine") ?? [];
    }

    public async Task<List<AvailableSeasonPassDto>> GetAvailablePassesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<AvailableSeasonPassDto>>("api/season-passes/available") ?? [];
    }

    public async Task<List<PastSeasonPassDto>> GetPastPassesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<PastSeasonPassDto>>("api/season-passes/past") ?? [];
    }

    public async Task<SeasonPassOptionsDto?> GetOptionsAsync(int seasonId)
    {
        var response = await httpClient.GetAsync($"api/season-passes/options?seasonId={seasonId}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<SeasonPassOptionsDto>();
    }

    public async Task<List<SeasonTeamDto>> GetSeasonTeamsAsync(int seasonId)
    {
        return await httpClient.GetFromJsonAsync<List<SeasonTeamDto>>($"api/season-passes/teams?seasonId={seasonId}") ?? [];
    }

    public async Task<(bool Success, string? ErrorMessage)> AcquireAsync(int seasonId)
    {
        var response = await httpClient.PostAsJsonAsync("api/season-passes/acquire", new AcquireSeasonPassRequest { SeasonId = seasonId });
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred while acquiring the season pass.";
            return (false, errorMessage);
        }
        catch
        {
            return (false, "An unexpected error occurred.");
        }
    }

    public async Task<(string? Url, string? ErrorMessage)> CreateCheckoutAsync(int seasonId)
    {
        var response = await httpClient.PostAsJsonAsync("api/season-passes/checkout", new CreateCheckoutSessionRequest { SeasonId = seasonId });
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CreateCheckoutSessionResponse>();
            return (result?.CheckoutUrl, null);
        }

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred while starting checkout.";
            return (null, errorMessage);
        }
        catch
        {
            return (null, "An unexpected error occurred.");
        }
    }
}
