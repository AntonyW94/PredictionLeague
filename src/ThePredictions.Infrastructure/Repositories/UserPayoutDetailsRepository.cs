using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class UserPayoutDetailsRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IUserPayoutDetailsRepository
{
    public async Task<UserPayoutDetails?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [UserId],
                [AccountName],
                [SortCode],
                [AccountNumber],
                [CreatedAtUtc],
                [UpdatedAtUtc]
            FROM
                [UserPayoutDetails]
            WHERE
                [UserId] = @UserId;";

        var command = new CommandDefinition(sql, new { UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.QuerySingleOrDefaultAsync<UserPayoutDetails>(command);
    }

    public async Task UpsertAsync(UserPayoutDetails payoutDetails, CancellationToken cancellationToken)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM [UserPayoutDetails] WHERE [UserId] = @UserId)
                UPDATE [UserPayoutDetails]
                SET
                    [AccountName] = @AccountName,
                    [SortCode] = @SortCode,
                    [AccountNumber] = @AccountNumber,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE
                    [UserId] = @UserId;
            ELSE
                INSERT INTO [UserPayoutDetails]
                (
                    [UserId],
                    [AccountName],
                    [SortCode],
                    [AccountNumber],
                    [CreatedAtUtc],
                    [UpdatedAtUtc]
                )
                VALUES
                (
                    @UserId,
                    @AccountName,
                    @SortCode,
                    @AccountNumber,
                    @CreatedAtUtc,
                    @UpdatedAtUtc
                );";

        var command = new CommandDefinition(sql, payoutDetails, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM [UserPayoutDetails] WHERE [UserId] = @UserId;";

        var command = new CommandDefinition(sql, new { UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}
