using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface IGameYearRepository
{
    Task<GameYear?> GetByIdAsync(int id);
    Task<GameYear?> GetActiveAsync();
    Task<IEnumerable<GameYear>> GetAllAsync();
    Task AddAsync(GameYear gameYear);
    Task UpdateAsync(GameYear gameYear);
}