using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class PasswordResetTokenRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext, IDateTimeProvider dateTimeProvider)
    : RepositoryBase(connectionFactory, transactionContext), IPasswordResetTokenRepository
{

    #region Create

    public async Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [PasswordResetTokens] ([Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc])
            VALUES (@Token, @UserId, @CreatedAtUtc, @ExpiresAtUtc)";

        var command = new CommandDefinition(sql, new
        {
            token.Token,
            token.UserId,
            token.CreatedAtUtc,
            token.ExpiresAtUtc
        }, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT [Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc]
            FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        var command = new CommandDefinition(sql, new { Token = token }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.QuerySingleOrDefaultAsync<PasswordResetToken>(command);
    }

    public async Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId AND [CreatedAtUtc] >= @SinceUtc";

        var command = new CommandDefinition(sql, new { UserId = userId, SinceUtc = sinceUtc }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteScalarAsync<int>(command);
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        var command = new CommandDefinition(sql, new { Token = token }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId";

        var command = new CommandDefinition(sql, new { UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [ExpiresAtUtc] < @NowUtc";

        var command = new CommandDefinition(sql, new { NowUtc = dateTimeProvider.UtcNow }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    public async Task<int> DeleteTokensOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [CreatedAtUtc] < @OlderThanUtc";

        var command = new CommandDefinition(sql, new { OlderThanUtc = olderThanUtc }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteAsync(command);
    }

    #endregion
}
