using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PasswordResetTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create

    public async Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [PasswordResetTokens] ([Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc])
            VALUES (@Token, @UserId, @CreatedAtUtc, @ExpiresAtUtc)";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
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

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PasswordResetToken>(sql, new { Token = token });
    }

    public async Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId AND [CreatedAtUtc] >= @SinceUtc";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, SinceUtc = sinceUtc });
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { Token = token });
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task DeleteExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [ExpiresAtUtc] < @NowUtc";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { NowUtc = DateTime.UtcNow });
    }

    public async Task<int> DeleteTokensOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [CreatedAtUtc] < @OlderThanUtc";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { OlderThanUtc = olderThanUtc });
    }

    #endregion
}
