using System.Diagnostics.CodeAnalysis;
using System.Data;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class TournamentRoundMappingRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ITournamentRoundMappingRepository
{
    public async Task<List<TournamentRoundMapping>> GetBySeasonIdAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                m.[Id],
                m.[SeasonId],
                m.[RoundNumber],
                m.[DisplayName],
                m.[Stages],
                m.[ExpectedMatchCount]
            FROM
                [TournamentRoundMappings] m
            WHERE
                m.[SeasonId] = @SeasonId
            ORDER BY
                m.[RoundNumber]";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SeasonId = seasonId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        var results = await Connection.QueryAsync<TournamentRoundMapping>(command);
        return results.ToList();
    }

    public async Task ReplaceAllForSeasonAsync(int seasonId, List<TournamentRoundMapping> mappings, CancellationToken cancellationToken)
    {
        const string deleteSql = @"
            DELETE FROM
                [TournamentRoundMappings]
            WHERE
                [SeasonId] = @SeasonId";

        const string insertSql = @"
            INSERT INTO [TournamentRoundMappings]
            (
                [SeasonId],
                [RoundNumber],
                [DisplayName],
                [Stages],
                [ExpectedMatchCount]
            )
            VALUES
            (
                @SeasonId,
                @RoundNumber,
                @DisplayName,
                @Stages,
                @ExpectedMatchCount
            )";

        await Connection.ExecuteAsync(
            new CommandDefinition(
                commandText: deleteSql,
                parameters: new { SeasonId = seasonId },
                transaction: Transaction,
                cancellationToken: cancellationToken));

        foreach (var mapping in mappings)
        {
            await Connection.ExecuteAsync(
                new CommandDefinition(
                    commandText: insertSql,
                    parameters: new
                    {
                        mapping.SeasonId,
                        mapping.RoundNumber,
                        mapping.DisplayName,
                        mapping.Stages,
                        mapping.ExpectedMatchCount
                    },
                    transaction: Transaction,
                    cancellationToken: cancellationToken));
        }
    }
}
