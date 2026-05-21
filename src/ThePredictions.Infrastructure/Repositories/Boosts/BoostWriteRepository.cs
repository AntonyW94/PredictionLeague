using Dapper;
using Microsoft.Data.SqlClient;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using System.Data;

namespace ThePredictions.Infrastructure.Repositories.Boosts;

public class BoostWriteRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IBoostWriteRepository
{
    public async Task<(bool Inserted, string? Error)> InsertUserBoostUsageAsync(
           string userId,
           int leagueId,
           int seasonId,
           int roundId,
           string boostCode,
           CancellationToken cancellationToken)
    {
        const string getBoostDefinitionSql = @"
            SELECT [Id]
            FROM [BoostDefinitions]
            WHERE [Code] = @BoostCode;";

        var boostDefinitionCommand = new CommandDefinition(getBoostDefinitionSql, new { BoostCode = boostCode }, transaction: Transaction, cancellationToken: cancellationToken);

        var boostId = await Connection.QuerySingleOrDefaultAsync<int?>(boostDefinitionCommand);
        if (boostId == null)
            return (false, "UnknownBoost");

        const string insertSql = "INSERT INTO [UserBoostUsages] (UserId, LeagueId, SeasonId, RoundId, BoostDefinitionId) VALUES (@UserId, @LeagueId, @SeasonId, @RoundId, @BoostDefinitionId);";

        var insertParams = new
        {
            UserId = userId,
            LeagueId = leagueId,
            SeasonId = seasonId,
            RoundId = roundId,
            BoostDefinitionId = boostId.Value
        };

        var insertCommand = new CommandDefinition(insertSql, insertParams, transaction: Transaction, cancellationToken: cancellationToken);

        try
        {
            await Connection.ExecuteAsync(insertCommand);
            return (true, null);
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // Unique constraint violation - boost already applied
            // Error 2627: UNIQUE CONSTRAINT violation
            // Error 2601: UNIQUE INDEX violation
            return (false, "Boost has already been applied to this round");
        }
    }

    public async Task<bool> DeleteUserBoostUsageAsync(string userId, int leagueId, int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
        DELETE FROM [UserBoostUsages]
        WHERE [UserId] = @UserId
          AND [LeagueId] = @LeagueId
          AND [RoundId] = @RoundId;";

        var command = new CommandDefinition(sql, new { UserId = userId, LeagueId = leagueId, RoundId = roundId }, transaction: Transaction, cancellationToken: cancellationToken);
        var affected = await Connection.ExecuteAsync(command);

        return affected > 0;
    }

    public async Task<int> AutoApplyUnusedBoostsForRoundAsync(int seasonId, int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [UserBoostUsages] ([UserId], [LeagueId], [SeasonId], [RoundId], [BoostDefinitionId])
            SELECT
                lm.[UserId],
                lm.[LeagueId],
                @SeasonId,
                @RoundId,
                lbr.[BoostDefinitionId]
            FROM
                [LeagueMembers] lm
                INNER JOIN [Leagues] l ON l.[Id] = lm.[LeagueId]
                INNER JOIN [LeagueBoostRules] lbr ON lbr.[LeagueId] = lm.[LeagueId]
            WHERE
                l.[SeasonId] = @SeasonId
                AND lm.[Status] = 'Approved'
                AND lbr.[IsEnabled] = 1
                AND lbr.[TotalUsesPerSeason] > 0
                AND NOT EXISTS (
                    SELECT 1
                    FROM [UserBoostUsages] ubu
                    WHERE ubu.[UserId] = lm.[UserId]
                        AND ubu.[LeagueId] = lm.[LeagueId]
                        AND ubu.[SeasonId] = @SeasonId
                        AND ubu.[BoostDefinitionId] = lbr.[BoostDefinitionId]
                );";

        var command = new CommandDefinition(
            sql,
            new { SeasonId = seasonId, RoundId = roundId },
            transaction: Transaction,
            cancellationToken: cancellationToken);

        return await Connection.ExecuteAsync(command);
    }
}
