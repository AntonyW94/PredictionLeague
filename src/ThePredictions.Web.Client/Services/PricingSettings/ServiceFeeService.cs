using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Web.Client.Services.PricingSettings;

[ExcludeFromCodeCoverage(Justification = "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply.")]
public class ServiceFeeService(HttpClient httpClient) : IServiceFeeService
{
    public async Task<List<ServiceFeeDto>> GetAllAsync()
    {
        return await httpClient.GetFromJsonAsync<List<ServiceFeeDto>>("api/admin/servicefees") ?? [];
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(string provider, UpdateServiceFeeRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/admin/servicefees/{provider}", request);

        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var message = errorContent?["errors"]?[0]?["ErrorMessage"]?.ToString()
                          ?? errorContent?["message"]?.ToString()
                          ?? "An error occurred while saving the service fee.";
            return (false, message);
        }
        catch
        {
            return (false, "An error occurred while saving the service fee.");
        }
    }
}
