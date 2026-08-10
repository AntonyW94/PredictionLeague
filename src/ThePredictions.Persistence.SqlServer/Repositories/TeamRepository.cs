using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class TeamRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ITeamRepository
{
    #region Create

    public async Task<Team> CreateAsync(Team team, CancellationToken cancellationToken)
    {
        const string sql = @"
                INSERT INTO [Teams]
                (
                    [Name],
                    [ShortName],
                    [LogoUrl],
                    [Abbreviation],
                    [ApiTeamId]
                )
                OUTPUT INSERTED.*
                VALUES
                (
                    @Name,
                    @ShortName,
                    @LogoUrl,
                    @Abbreviation,
                    @ApiTeamId
                );";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: team,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleAsync<Team>(command);
    }

    #endregion

    #region Read

    public async Task<Team?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT
                    [Id],
                    [Name],
                    [ShortName],
                    [LogoUrl],
                    [Abbreviation],
                    [ApiTeamId]
                FROM [Teams]
                WHERE [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleOrDefaultAsync<Team>(command);
    }

    public async Task<Team?> GetByApiIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT
                    [Id],
                    [Name],
                    [ShortName],
                    [LogoUrl],
                    [Abbreviation],
                    [ApiTeamId]
                FROM [Teams]
                WHERE [ApiTeamId] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleOrDefaultAsync<Team>(command);
    }

    public async Task<Dictionary<int, Team>> GetByApiIdsAsync(IEnumerable<int> apiIds, CancellationToken cancellationToken)
    {
        var apiIdsList = apiIds.ToList();
        if (!apiIdsList.Any())
            return new Dictionary<int, Team>();

        const string sql = @"
                SELECT
                    t.[Id],
                    t.[Name],
                    t.[ShortName],
                    t.[LogoUrl],
                    t.[Abbreviation],
                    t.[ApiTeamId]
                FROM
                    [Teams] t
                WHERE
                    t.[ApiTeamId] IN @ApiIds;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { ApiIds = apiIdsList },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        var teams = await Connection.QueryAsync<Team>(command);
        return teams
            .Where(t => t.ApiTeamId.HasValue)
            .ToDictionary(t => t.ApiTeamId!.Value);
    }

    #endregion

    #region Update

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken)
    {
        const string sql = @"
                UPDATE [Teams]
                SET
                    [Name] = @Name,
                    [ShortName] = @ShortName,
                    [LogoUrl] = @LogoUrl,
                    [Abbreviation] = @Abbreviation,
                    [ApiTeamId] = @ApiTeamId
                WHERE [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: team,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
