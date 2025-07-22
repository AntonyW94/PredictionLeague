using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRefreshTokenRepository
{
    #region Create

    Task CreateAsync(RefreshToken token, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken);
   
    #endregion
}