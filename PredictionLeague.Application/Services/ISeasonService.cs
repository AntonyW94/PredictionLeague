using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Services;

public interface ISeasonService
{
    Task CreateAsync(CreateSeasonRequest request);
    Task<IEnumerable<SeasonDto>> GetAllAsync();
    Task<SeasonDto?> GetByIdAsync(int id);
    Task UpdateAsync(int id, UpdateSeasonRequest request);
}