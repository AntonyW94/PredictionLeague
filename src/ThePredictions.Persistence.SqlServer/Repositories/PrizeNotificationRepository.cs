using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class PrizeNotificationRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IPrizeNotificationRepository
{
    public async Task AddNotificationsAsync(IEnumerable<PrizeNotification> notifications, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [PrizeNotifications]
            (
                [UserId],
                [LeaguePrizeSettingId],
                [RoundNumber],
                [Month],
                [SentAtUtc]
            )
            SELECT
                src.[UserId],
                src.[LeaguePrizeSettingId],
                src.[RoundNumber],
                src.[Month],
                src.[SentAtUtc]
            FROM
                OPENJSON(@Rows)
                WITH (
                    [UserId] nvarchar(4000) 'strict $.UserId',
                    [LeaguePrizeSettingId] int 'strict $.LeaguePrizeSettingId',
                    [RoundNumber] int 'strict $.RoundNumber',
                    [Month] int 'strict $.Month',
                    [SentAtUtc] datetime2 'strict $.SentAtUtc'
                ) src;";

        var rows = notifications
            .Select(notification => new
            {
                notification.UserId,
                notification.LeaguePrizeSettingId,
                notification.RoundNumber,
                notification.Month,
                notification.SentAtUtc
            })
            .ToList();

        if (rows.Count == 0)
            return;

        var command = new CommandDefinition(commandText: sql, parameters: new { Rows = JsonRows.From(rows) }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }
}
