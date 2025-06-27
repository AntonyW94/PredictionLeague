using PredictionLeague.Core.Models;
using PredictionLeague.Shared.Admin.Seasons;

namespace PredictionLeague.Core.Repositories;

public interface ISeasonRepository
{
    Task<IEnumerable<SeasonDto>> GetAllAsync();
    Task<SeasonDto?> GetByIdAsync(int id);
    Task AddAsync(Season season);
    Task UpdateAsync(Season season);
}