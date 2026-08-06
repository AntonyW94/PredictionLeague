using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ThePredictions.Contracts.Admin.EmailSettings;

namespace ThePredictions.Web.Client.Services.EmailSettings;

[ExcludeFromCodeCoverage(Justification = "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply.")]
public class EmailSettingsService(HttpClient httpClient) : IEmailSettingsService
{
    public async Task<EmailSettingsDto?> GetAsync()
    {
        return await httpClient.GetFromJsonAsync<EmailSettingsDto>("api/admin/emailsettings");
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateEmailSettingsRequest request)
    {
        var response = await httpClient.PutAsJsonAsync("api/admin/emailsettings", request);

        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
            var message = errorContent?["errors"]?[0]?["ErrorMessage"]?.ToString()
                          ?? errorContent?["message"]?.ToString()
                          ?? "An error occurred while saving the email settings.";
            return (false, message);
        }
        catch
        {
            return (false, "An error occurred while saving the email settings.");
        }
    }
}
