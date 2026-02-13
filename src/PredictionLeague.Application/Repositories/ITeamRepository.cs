using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ITeamRepository
{
    #region Create

    Task<Team> CreateAsync(Team team, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<Team?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Team?> GetByApiIdAsync(int apiId, CancellationToken cancellationToken);
    Task<Dictionary<int, Team>> GetByApiIdsAsync(IEnumerable<int> apiIds, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(Team team, CancellationToken cancellationToken);

    #endregion
}