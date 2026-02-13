using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public TeamRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

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
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}