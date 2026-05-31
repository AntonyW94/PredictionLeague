using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Web.Client.Services.SeasonPasses;

public interface ISeasonPassService
{
    Task<List<MySeasonPassDto>> GetMyPassesAsync();
    Task<List<AvailableSeasonPassDto>> GetAvailablePassesAsync();
    Task<List<PastSeasonPassDto>> GetPastPassesAsync();
    Task<SeasonPassOptionsDto?> GetOptionsAsync(int seasonId);
    Task<List<SeasonTeamDto>> GetSeasonTeamsAsync(int seasonId);
    Task<(bool Success, string? ErrorMessage)> AcquireAsync(int seasonId);
}
