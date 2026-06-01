using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Web.Client.Services.PricingSettings;

public interface IServiceFeeService
{
    Task<List<ServiceFeeDto>> GetAllAsync();
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(string provider, UpdateServiceFeeRequest request);
}
