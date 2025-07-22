using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IMatchRepository
{
    #region Read 
    
    Task<IEnumerable<Match>> GetByRoundIdAsync(int roundId, CancellationToken cancellationToken);

    #endregion
}