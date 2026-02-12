using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class PasswordResetTokenRepository(IDbConnectionFactory connectionFactory, IDateTimeProvider dateTimeProvider) : IPasswordResetTokenRepository
{
    private IDbConnection Connection => connectionFactory.CreateConnection();


    #region Create

    public async Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [PasswordResetTokens] ([Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc])
            VALUES (@Token, @UserId, @CreatedAtUtc, @ExpiresAtUtc)";

        await Connection.ExecuteAsync(sql, new
        {
            token.Token,
            token.UserId,
            token.CreatedAtUtc,
            token.ExpiresAtUtc
        });
    }

    #endregion

    #region Read

    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT [Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc]
            FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        return await Connection.QuerySingleOrDefaultAsync<PasswordResetToken>(sql, new { Token = token });
    }

    public async Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId AND [CreatedAtUtc] >= @SinceUtc";

        return await Connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, SinceUtc = sinceUtc });
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        await Connection.ExecuteAsync(sql, new { Token = token });
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId";

        await Connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task DeleteExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [ExpiresAtUtc] < @NowUtc";

        await Connection.ExecuteAsync(sql, new { NowUtc = dateTimeProvider.UtcNow });
    }

    public async Task<int> DeleteTokensOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [CreatedAtUtc] < @OlderThanUtc";

        return await Connection.ExecuteAsync(sql, new { OlderThanUtc = olderThanUtc });
    }

    #endregion
}
