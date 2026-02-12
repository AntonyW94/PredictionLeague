using Dapper;
using Microsoft.Data.SqlClient;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories.Boosts;

public class BoostWriteRepository(IDbConnectionFactory connectionFactory) : IBoostWriteRepository
{
    private IDbConnection Connection => connectionFactory.CreateConnection();

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

        var boostDefinitionCommand = new CommandDefinition(getBoostDefinitionSql, new { BoostCode = boostCode }, cancellationToken: cancellationToken);

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

        var insertCommand = new CommandDefinition(insertSql, insertParams, cancellationToken: cancellationToken);

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

        var command = new CommandDefinition(sql, new { UserId = userId, LeagueId = leagueId, RoundId = roundId }, cancellationToken: cancellationToken);
        var affected = await Connection.ExecuteAsync(command);

        return affected > 0;
    }
}