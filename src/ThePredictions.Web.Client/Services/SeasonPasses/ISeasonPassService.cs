using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Web.Client.Services.SeasonPasses;

public interface ISeasonPassService
{
    Task<List<MySeasonPassDto>> GetMyPassesAsync();
    Task<List<AvailableSeasonPassDto>> GetAvailablePassesAsync();
    Task<(bool Success, string? ErrorMessage)> AcquireAsync(int seasonId);
}
