using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IUserPredictionRepository
{
    #region Create
    
    Task UpsertBatchAsync(IEnumerable<UserPrediction> predictions, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<IEnumerable<UserPrediction>> GetByMatchIdsAsync(IEnumerable<int> matchIds, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateOutcomesAsync(IEnumerable<UserPrediction> predictionsToUpdate, CancellationToken cancellationToken);

    #endregion
}