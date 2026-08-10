using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class RefreshTokenRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IRefreshTokenRepository
{
    #region Create

    public async Task CreateAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO RefreshTokens (UserId, Token, Expires, Created) VALUES (@UserId, @Token, @Expires, @Created)";

        var command = new CommandDefinition(sql, token, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM RefreshTokens WHERE Token = @Token";

        var command = new CommandDefinition(sql, new { Token = token }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.QuerySingleOrDefaultAsync<RefreshToken>(command);
    }

    #endregion

    #region Update

    public Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE [RefreshTokens] SET [Revoked] = GETUTCDATE() WHERE [UserId] = @UserId AND [Revoked] IS NULL;";

        var command = new CommandDefinition(sql, new { UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);
        return Connection.ExecuteAsync(command);
    }

    public async Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE [RefreshTokens]
            SET
                [Revoked] = @Revoked
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(sql, token, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    #endregion
}
