using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class LeaguePayoutRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeaguePayoutRepository
{
    public async Task<LeaguePayout?> GetByLeagueAndUserAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [Id],
                [LeagueId],
                [UserId],
                [TotalAmount],
                [PaidAtUtc],
                [CreatedAtUtc],
                [UpdatedAtUtc]
            FROM
                [LeaguePayouts]
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId;";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.QuerySingleOrDefaultAsync<LeaguePayout>(command);
    }

    public async Task UpsertAsync(LeaguePayout payout, CancellationToken cancellationToken)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM [LeaguePayouts] WHERE [LeagueId] = @LeagueId AND [UserId] = @UserId)
                UPDATE [LeaguePayouts]
                SET
                    [TotalAmount] = @TotalAmount,
                    [PaidAtUtc] = @PaidAtUtc,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE
                    [LeagueId] = @LeagueId
                    AND [UserId] = @UserId;
            ELSE
                INSERT INTO [LeaguePayouts]
                (
                    [LeagueId],
                    [UserId],
                    [TotalAmount],
                    [PaidAtUtc],
                    [CreatedAtUtc],
                    [UpdatedAtUtc]
                )
                VALUES
                (
                    @LeagueId,
                    @UserId,
                    @TotalAmount,
                    @PaidAtUtc,
                    @CreatedAtUtc,
                    @UpdatedAtUtc
                );";

        var command = new CommandDefinition(sql, payout, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}
