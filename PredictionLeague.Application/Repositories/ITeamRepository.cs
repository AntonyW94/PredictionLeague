using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ITeamRepository
{
    #region Create

    Task<Team> CreateAsync(Team team);

    #endregion

    #region Read

    Task<IEnumerable<Team>> GetAllAsync(); 
    Task<Team?> GetByIdAsync(int id);

    #endregion

    #region Update
    
    Task UpdateAsync(Team team);

    #endregion
}