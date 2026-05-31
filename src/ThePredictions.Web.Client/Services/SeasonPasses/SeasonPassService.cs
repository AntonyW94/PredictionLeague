using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Web.Client.Services.SeasonPasses;

public class SeasonPassService(HttpClient httpClient) : ISeasonPassService
{
    public async Task<List<MySeasonPassDto>> GetMyPassesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<MySeasonPassDto>>("api/seasonpasses/mine") ?? [];
    }

    public async Task<List<AvailableSeasonPassDto>> GetAvailablePassesAsync()
    {
        return await httpClient.GetFromJsonAsync<List<AvailableSeasonPassDto>>("api/seasonpasses/available") ?? [];
    }

    public async Task<(bool Success, string? ErrorMessage)> AcquireAsync(int seasonId)
    {
        var response = await httpClient.PostAsJsonAsync("api/seasonpasses/acquire", new AcquireSeasonPassRequest { SeasonId = seasonId });
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
}
