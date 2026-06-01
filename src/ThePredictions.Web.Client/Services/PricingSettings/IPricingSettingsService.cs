using ThePredictions.Contracts.Admin.PricingSettings;

namespace ThePredictions.Web.Client.Services.PricingSettings;

public interface IPricingSettingsService
{
    Task<PricingSettingsDto?> GetAsync();
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdatePricingSettingsRequest request);
}
