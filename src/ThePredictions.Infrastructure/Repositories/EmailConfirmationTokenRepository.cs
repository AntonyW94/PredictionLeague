using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class EmailConfirmationTokenRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IEmailConfirmationTokenRepository
{
    public async Task CreateAsync(EmailConfirmationToken token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [EmailConfirmationTokens] ([Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc])
            VALUES (@Token, @UserId, @CreatedAtUtc, @ExpiresAtUtc);";

        var command = new CommandDefinition(sql, new
        {
            token.Token,
            token.UserId,
            token.CreatedAtUtc,
            token.ExpiresAtUtc
        }, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task<EmailConfirmationToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT [Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc]
            FROM [EmailConfirmationTokens]
            WHERE [Token] = @Token;";

        var command = new CommandDefinition(sql, new { Token = token }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.QuerySingleOrDefaultAsync<EmailConfirmationToken>(command);
    }

    public async Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [EmailConfirmationTokens]
            WHERE [UserId] = @UserId AND [CreatedAtUtc] >= @SinceUtc;";

        var command = new CommandDefinition(sql, new { UserId = userId, SinceUtc = sinceUtc }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [EmailConfirmationTokens]
            WHERE [UserId] = @UserId;";

        var command = new CommandDefinition(sql, new { UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }
}
