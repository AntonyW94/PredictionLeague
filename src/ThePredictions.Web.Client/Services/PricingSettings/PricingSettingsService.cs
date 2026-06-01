using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.Admin.PricingSettings;

namespace ThePredictions.Web.Client.Services.PricingSettings;

public class PricingSettingsService(HttpClient httpClient) : IPricingSettingsService
{
    public async Task<PricingSettingsDto?> GetAsync()
    {
        return await httpClient.GetFromJsonAsync<PricingSettingsDto>("api/admin/pricingsettings");
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdatePricingSettingsRequest request)
    {
        var response = await httpClient.PutAsJsonAsync("api/admin/pricingsettings", request);

        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var message = errorContent?["errors"]?[0]?["ErrorMessage"]?.ToString()
                          ?? errorContent?["message"]?.ToString()
                          ?? "An error occurred while saving the pricing settings.";
            return (false, message);
        }
        catch
        {
            return (false, "An error occurred while saving the pricing settings.");
        }
    }
}
