using PredictionLeague.Shared.Admin.Seasons;

namespace PredictionLeague.Core.Services;

public interface ISeasonService
{
    Task<IEnumerable<SeasonDto>> GetAllAsync();
    Task<SeasonDto?> GetByIdAsync(int id);
}