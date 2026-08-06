using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Infrastructure.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class LeagueMemberRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueMemberRepository
{
    public async Task<LeagueMember?> GetAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [LeagueId],
                [UserId],
                [Status],
                [IsAlertDismissed],
                [IsArchivedByUser],
                [JoinedAtUtc],
                [ApprovedAtUtc]
            FROM
                [LeagueMembers]
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.QueryFirstOrDefaultAsync<LeagueMember>(command);
    }

    public async Task UpdateAsync(LeagueMember member, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [LeagueMembers]
            SET
                [Status] = @Status,
                [IsAlertDismissed] = @IsAlertDismissed,
                [IsArchivedByUser] = @IsArchivedByUser,
                [ApprovedAtUtc] = @ApprovedAtUtc
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId";

        var command = new CommandDefinition(sql, new
        {
            Status = member.Status.ToString(),
            member.IsAlertDismissed,
            member.IsArchivedByUser,
            member.ApprovedAtUtc,
            member.LeagueId,
            member.UserId
        }, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteAsync(LeagueMember member, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE FROM
                [LeagueMembers]
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId";

        var command = new CommandDefinition(sql, new
        {
            member.LeagueId,
            member.UserId
        }, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}
