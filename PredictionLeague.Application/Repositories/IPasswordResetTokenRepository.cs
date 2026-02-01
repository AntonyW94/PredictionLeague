using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IPasswordResetTokenRepository
{
    #region Create

    /// <summary>
    /// Stores a new password reset token.
    /// </summary>
    Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    #endregion

    #region Read

    /// <summary>
    /// Retrieves a token by its value. Returns null if not found.
    /// </summary>
    Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tokens created for a user since the specified time (for rate limiting).
    /// </summary>
    Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default);

    #endregion

    #region Delete

    /// <summary>
    /// Deletes a specific token (after successful password reset).
    /// </summary>
    Task DeleteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all tokens for a user (when creating a new one or after successful reset).
    /// </summary>
    Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all expired tokens (cleanup).
    /// </summary>
    Task DeleteExpiredTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all tokens created before the specified date (for scheduled cleanup).
    /// </summary>
    /// <returns>Number of tokens deleted.</returns>
    Task<int> DeleteTokensOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);

    #endregion
}
