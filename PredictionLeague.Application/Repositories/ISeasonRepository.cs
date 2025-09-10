using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ISeasonRepository
{
    #region Create

    Task<Season> CreateAsync(Season season, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(Season request, CancellationToken cancellationToken);
    
    #endregion
}