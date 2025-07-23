using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IUserPredictionRepository
{
    #region Create
    
    Task UpsertBatchAsync(IEnumerable<UserPrediction> predictions, CancellationToken cancellationToken);
    
    #endregion
}