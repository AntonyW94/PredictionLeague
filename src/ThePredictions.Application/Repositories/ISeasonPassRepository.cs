using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface ISeasonPassRepository
{
    #region Create

    Task AddAsync(SeasonPass seasonPass, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<bool> ExistsForUserSeasonAsync(string userId, int seasonId, CancellationToken cancellationToken);
    Task<int> CountForUserAsync(string userId, CancellationToken cancellationToken);

    #endregion
}
