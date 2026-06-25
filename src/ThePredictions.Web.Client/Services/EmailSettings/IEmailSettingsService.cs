using ThePredictions.Contracts.Admin.EmailSettings;

namespace ThePredictions.Web.Client.Services.EmailSettings;

public interface IEmailSettingsService
{
    Task<EmailSettingsDto?> GetAsync();
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateEmailSettingsRequest request);
}
