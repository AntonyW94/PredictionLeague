using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories
{
    public interface IMatchRepository
    {
        Task<Match?> GetByIdAsync(int id);
        Task<IEnumerable<Match>> GetByGameWeekIdAsync(int gameWeekId);
        Task AddAsync(Match match);
        Task UpdateAsync(Match match);
    }
}
