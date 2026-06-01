using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Web.Client.Services.RunningCosts;

public class RunningCostService(HttpClient httpClient) : IRunningCostService
{
    public async Task<List<RunningCostDto>> GetAllAsync()
    {
        return await httpClient.GetFromJsonAsync<List<RunningCostDto>>("api/admin/runningcosts") ?? [];
    }

    public async Task<(bool Success, string? ErrorMessage)> CreateAsync(SaveRunningCostRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/admin/runningcosts", request);
        return await ToResultAsync(response);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(int id, SaveRunningCostRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/admin/runningcosts/{id}", request);
        return await ToResultAsync(response);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var response = await httpClient.DeleteAsync($"api/admin/runningcosts/{id}");
        return response.IsSuccessStatusCode;
    }

    private static async Task<(bool Success, string? ErrorMessage)> ToResultAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var message = errorContent?["errors"]?[0]?["ErrorMessage"]?.ToString()
                          ?? errorContent?["message"]?.ToString()
                          ?? "An error occurred while saving the running cost.";
            return (false, message);
        }
        catch
        {
            return (false, "An error occurred while saving the running cost.");
        }
    }
}
