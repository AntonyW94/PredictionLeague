using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class UserBadgeRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IUserBadgeRepository
{
    public async Task<bool> AwardAsync(AwardedBadge badge, CancellationToken cancellationToken)
    {
        // Null-safe existence check: for lifetime badges RoundId/SeasonId are NULL, and `[RoundId] = @RoundId`
        // is never true for NULLs in SQL, so we match NULLs explicitly. This mirrors the unique index
        // (UserId, BadgeKey, RoundId, SeasonId), where SQL Server treats NULLs as equal.
        const string sql = @"
                IF NOT EXISTS (
                    SELECT 1
                    FROM [UserBadges]
                    WHERE [UserId] = @UserId
                        AND [BadgeKey] = @BadgeKey
                        AND ((@RoundId IS NULL AND [RoundId] IS NULL) OR [RoundId] = @RoundId)
                        AND ((@SeasonId IS NULL AND [SeasonId] IS NULL) OR [SeasonId] = @SeasonId)
                )
                    INSERT INTO [UserBadges] ([UserId], [BadgeKey], [AwardedUtc], [LeagueId], [RoundId], [SeasonId], [Detail])
                    VALUES (@UserId, @BadgeKey, @AwardedUtc, @LeagueId, @RoundId, @SeasonId, @Detail);

                SELECT @@ROWCOUNT;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                badge.UserId,
                badge.BadgeKey,
                badge.AwardedUtc,
                badge.LeagueId,
                badge.RoundId,
                badge.SeasonId,
                badge.Detail
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        var rowsAffected = await Connection.ExecuteScalarAsync<int>(command);
        return rowsAffected > 0;
    }
}
