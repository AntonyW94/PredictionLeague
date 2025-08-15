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
                    [IsActive],
                    [NumberOfRounds]
                )
                VALUES
                (
                    @Name,
                    @StartDate,
                    @EndDate,
                    @IsActive,
                    @NumberOfRounds
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: season,
            cancellationToken: cancellationToken
        );

        var newSeasonId = await Connection.ExecuteScalarAsync<int>(command);

        typeof(Season).GetProperty(nameof(Season.Id))?.SetValue(season, newSeasonId);
        return season;
    }

    #endregion

    #region Read

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

    public async Task<int> GetRoundCountAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT [NumberOfRounds] FROM [Seasons] WHERE [Id] = @seasonId;";
       
        var command = new CommandDefinition(
            sql,
            new { seasonId },
            cancellationToken: cancellationToken
        );
        
        return await Connection.ExecuteScalarAsync<int>(command);
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
                    [IsActive] = @IsActive,
                    [NumberOfRounds] = @NumberOfRounds
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