using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ISeasonRepository
{
    Task<IEnumerable<Season>> GetAllAsync();
    Task<Season?> GetByIdAsync(int id);
    Task AddAsync(Season season);
    Task UpdateAsync(Season request);
}