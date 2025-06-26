using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(int id);
    Task<Season?> GetActiveAsync();
    Task<IEnumerable<Season>> GetAllAsync();
    Task AddAsync(Season season);
    Task UpdateAsync(Season season);
}