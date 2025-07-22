using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRefreshTokenRepository
{
    #region Create

    Task CreateAsync(RefreshToken token);

    #endregion

    #region Read

    Task<RefreshToken?> GetByTokenAsync(string token);

    #endregion

    #region Update

    Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken);
   
    #endregion
}