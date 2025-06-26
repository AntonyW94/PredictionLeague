using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface IRoundResultRepository
{
    Task<IEnumerable<RoundResult>> GetByRoundIdAsync(int roundId);
    Task UpsertAsync(RoundResult result);
}