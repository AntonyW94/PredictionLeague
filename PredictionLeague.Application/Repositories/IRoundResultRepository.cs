using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundResultRepository
{
    Task<IEnumerable<RoundResult>> GetByRoundIdAsync(int roundId);
    Task UpsertAsync(RoundResult result);
}