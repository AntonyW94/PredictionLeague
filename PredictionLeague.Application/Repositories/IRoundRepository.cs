using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundRepository
{
    #region Create

    Task<Round> CreateAsync(Round round);

    #endregion

    #region Read
    
    Task<IEnumerable<Round>> GetBySeasonIdAsync(int seasonId);
    Task<Round?> GetCurrentRoundAsync(int seasonId);
    Task<Round?> GetByIdAsync(int id);

    #endregion

    #region Update
    
    Task UpdateAsync(Round round);

    #endregion
}