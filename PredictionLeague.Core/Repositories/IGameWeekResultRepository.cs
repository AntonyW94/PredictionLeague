using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface IGameWeekResultRepository
{
    Task<IEnumerable<GameWeekResult>> GetByGameWeekIdAsync(int gameWeekId);
    Task UpsertAsync(GameWeekResult result);
}