using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ISeasonRepository
{
    #region Create

    Task<Season> CreateAsync(Season season);

    #endregion

    #region Read

    Task<IEnumerable<Season>> GetAllAsync();
    Task<Season?> GetByIdAsync(int id);

    #endregion

    #region Update
    
    Task UpdateAsync(Season request);
    
    #endregion
}