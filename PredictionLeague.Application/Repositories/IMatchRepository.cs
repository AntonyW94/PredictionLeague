using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(int id);
    Task<IEnumerable<Match>> GetByRoundIdAsync(int roundId);
    Task AddAsync(Match match);
    Task UpdateAsync(Match match);
    Task DeleteByRoundIdAsync(int roundId);
}