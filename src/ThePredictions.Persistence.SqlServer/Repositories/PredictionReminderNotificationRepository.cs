using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class PredictionReminderNotificationRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IPredictionReminderNotificationRepository
{
    public async Task<IReadOnlyDictionary<string, DateTime>> GetLastRemindedUtcAsync(int roundId, IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<string, DateTime>();

        const string sql = @"
            SELECT
                prn.[UserId],
                prn.[LastRemindedUtc]
            FROM
                [PredictionReminderNotifications] prn
            WHERE
                prn.[RoundId] = @RoundId
                AND prn.[UserId] IN @UserIds;";

        var command = new CommandDefinition(commandText: sql, parameters: new { RoundId = roundId, UserIds = ids }, transaction: Transaction, cancellationToken: cancellationToken);
        var rows = await Connection.QueryAsync<(string UserId, DateTime LastRemindedUtc)>(command);

        return rows.ToDictionary(r => r.UserId, r => r.LastRemindedUtc);
    }

    public async Task UpsertAsync(PredictionReminderNotification notification, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE [PredictionReminderNotifications]
            SET
                [LastRemindedUtc] = @LastRemindedUtc,
                [RemindedByUserId] = @RemindedByUserId
            WHERE
                [RoundId] = @RoundId
                AND [UserId] = @UserId;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO [PredictionReminderNotifications]
                (
                    [RoundId],
                    [UserId],
                    [LastRemindedUtc],
                    [RemindedByUserId]
                )
                VALUES
                (
                    @RoundId,
                    @UserId,
                    @LastRemindedUtc,
                    @RemindedByUserId
                );
            END";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                notification.RoundId,
                notification.UserId,
                notification.LastRemindedUtc,
                notification.RemindedByUserId
            },
            transaction: Transaction,
            cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}
