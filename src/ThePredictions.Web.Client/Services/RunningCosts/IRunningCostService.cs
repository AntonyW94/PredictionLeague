using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Web.Client.Services.RunningCosts;

public interface IRunningCostService
{
    Task<List<RunningCostDto>> GetAllAsync();
    Task<(bool Success, string? ErrorMessage)> CreateAsync(SaveRunningCostRequest request);
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(int id, SaveRunningCostRequest request);
    Task<bool> DeleteAsync(int id);
}
