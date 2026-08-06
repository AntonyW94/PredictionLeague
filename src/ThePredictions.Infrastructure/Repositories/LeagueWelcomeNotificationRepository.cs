using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class LeagueWelcomeNotificationRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueWelcomeNotificationRepository
{
    public async Task AddNotificationsAsync(IEnumerable<LeagueWelcomeNotification> notifications, CancellationToken cancellationToken)
    {
        if (!notifications.Any())
            return;

        const string sql = @"
            INSERT INTO [LeagueWelcomeNotifications]
            (
                [LeagueId],
                [UserId],
                [SentAtUtc]
            )
            VALUES
            (
                @LeagueId,
                @UserId,
                @SentAtUtc
            );";

        var command = new CommandDefinition(commandText: sql, parameters: notifications, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }
}
