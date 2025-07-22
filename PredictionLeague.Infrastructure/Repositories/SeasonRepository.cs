using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public SeasonRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create

    public async Task<Season> CreateAsync(Season season, CancellationToken cancellationToken)
    {
        const string sql = @"
                INSERT INTO [Seasons]
                (
                    [Name],
                    [StartDate],
                    [EndDate],
                    [IsActive]
                )
                VALUES
                (
                    @Name,
                    @StartDate,
                    @EndDate,
                    @IsActive
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                season.Name,
                season.StartDate,
                season.EndDate,
                season.IsActive
            },
            cancellationToken: cancellationToken
        );

        var newSeasonId = await Connection.ExecuteScalarAsync<int>(command);

        typeof(Season).GetProperty(nameof(Season.Id))?.SetValue(season, newSeasonId);
        return season;
    }

    #endregion

    #region Read

    public async Task<IEnumerable<Season>> FetchAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM [Seasons] ORDER BY [StartDate] DESC;";
      
        var command = new CommandDefinition(
            commandText: sql,
            cancellationToken: cancellationToken
        );
        
        return await Connection.QueryAsync<Season>(command);
    }

    public async Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM [Seasons] WHERE [Id] = @Id;";
       
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id },
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleOrDefaultAsync<Season>(command);
    }

    #endregion

    #region Update

    public async Task UpdateAsync(Season season, CancellationToken cancellationToken)
    {
        const string sql = @"
                UPDATE [Seasons]
                SET
                    [Name] = @Name,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [IsActive] = @IsActive
                WHERE [Id] = @Id;";
       
        var command = new CommandDefinition(
            commandText: sql,
            parameters: season,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}