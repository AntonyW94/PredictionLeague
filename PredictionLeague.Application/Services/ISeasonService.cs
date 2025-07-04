using PredictionLeague.Shared.Admin.Seasons;

namespace PredictionLeague.Application.Services;

public interface ISeasonService
{
    Task<IEnumerable<SeasonDto>> GetAllAsync();
    Task<SeasonDto?> GetByIdAsync(int id);
}