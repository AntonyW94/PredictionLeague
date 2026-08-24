using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class LeagueWelcomeNotificationRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueWelcomeNotificationRepository
{
    public async Task AddNotificationsAsync(IEnumerable<LeagueWelcomeNotification> notifications, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [LeagueWelcomeNotifications]
            (
                [LeagueId],
                [UserId],
                [SentAtUtc]
            )
            SELECT
                src.[LeagueId],
                src.[UserId],
                src.[SentAtUtc]
            FROM
                OPENJSON(@Rows)
                WITH (
                    [LeagueId] int 'strict $.LeagueId',
                    [UserId] nvarchar(4000) 'strict $.UserId',
                    [SentAtUtc] datetime2 'strict $.SentAtUtc'
                ) src;";

        var rows = notifications
            .Select(notification => new
            {
                notification.LeagueId,
                notification.UserId,
                notification.SentAtUtc
            })
            .ToList();

        if (rows.Count == 0)
            return;

        var command = new CommandDefinition(commandText: sql, parameters: new { Rows = JsonRows.From(rows) }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }
}
