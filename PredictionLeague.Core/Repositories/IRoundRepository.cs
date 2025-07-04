using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface IRoundRepository
{
    Task<Round> AddAsync(Round round);
    Task<IEnumerable<Round>> GetBySeasonIdAsync(int seasonId);
    Task<Round?> GetCurrentRoundAsync(int seasonId);
    Task<Round?> GetByIdAsync(int id);
    Task UpdateAsync(Round round);
}