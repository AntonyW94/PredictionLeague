using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundRepository
{
    #region Create

    Task<Round> CreateAsync(Round round, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken);

    #endregion

    #region Update
    
    Task UpdateAsync(Round round, CancellationToken cancellationToken);

    #endregion
}