using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class PrizeNotificationRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IPrizeNotificationRepository
{
    public async Task AddNotificationsAsync(IEnumerable<PrizeNotification> notifications, CancellationToken cancellationToken)
    {
        if (!notifications.Any())
            return;

        const string sql = @"
            INSERT INTO [PrizeNotifications]
            (
                [UserId],
                [LeaguePrizeSettingId],
                [RoundNumber],
                [Month],
                [SentAtUtc]
            )
            VALUES
            (
                @UserId,
                @LeaguePrizeSettingId,
                @RoundNumber,
                @Month,
                @SentAtUtc
            );";

        var command = new CommandDefinition(commandText: sql, parameters: notifications, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }
}
