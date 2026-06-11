using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

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
