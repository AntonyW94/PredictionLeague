using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class SeasonPassRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ISeasonPassRepository
{
    #region Create

    public async Task AddAsync(SeasonPass seasonPass, CancellationToken cancellationToken)
    {
        const string sql = @"
                INSERT INTO [SeasonPasses]
                (
                    [UserId],
                    [SeasonId],
                    [Tier],
                    [Source],
                    [AmountPaid],
                    [SmsFeePaid],
                    [StripePaymentReference],
                    [CreatedAtUtc],
                    [SmsSentCount],
                    [RewardRedeemedForSeasonId]
                )
                VALUES
                (
                    @UserId,
                    @SeasonId,
                    @Tier,
                    @Source,
                    @AmountPaid,
                    @SmsFeePaid,
                    @StripePaymentReference,
                    @CreatedAtUtc,
                    @SmsSentCount,
                    @RewardRedeemedForSeasonId
                );";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                seasonPass.UserId,
                seasonPass.SeasonId,
                Tier = seasonPass.Tier.ToString(),
                Source = seasonPass.Source.ToString(),
                seasonPass.AmountPaid,
                seasonPass.SmsFeePaid,
                seasonPass.StripePaymentReference,
                seasonPass.CreatedAtUtc,
                seasonPass.SmsSentCount,
                seasonPass.RewardRedeemedForSeasonId
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<bool> ExistsForUserSeasonAsync(string userId, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM [SeasonPasses] sp
                    WHERE sp.[UserId] = @UserId
                        AND sp.[SeasonId] = @SeasonId
                ) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { UserId = userId, SeasonId = seasonId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<int> CountForUserAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT COUNT(*)
                FROM [SeasonPasses] sp
                WHERE sp.[UserId] = @UserId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { UserId = userId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<int>(command);
    }

    #endregion
}
