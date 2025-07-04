using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundRepository
{
    Task<Round> AddAsync(Round round);
    Task<IEnumerable<Round>> GetBySeasonIdAsync(int seasonId);
    Task<Round?> GetCurrentRoundAsync(int seasonId);
    Task<Round?> GetByIdAsync(int id);
    Task UpdateAsync(Round round);
}