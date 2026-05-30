using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface ICompetitionRepository
{
    #region Create

    Task<Competition> CreateAsync(Competition competition, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<Competition?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Competition?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> HasSeasonsAsync(int competitionId, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(Competition competition, CancellationToken cancellationToken);

    #endregion

    #region Delete

    Task DeleteAsync(int competitionId, CancellationToken cancellationToken);

    #endregion
}
