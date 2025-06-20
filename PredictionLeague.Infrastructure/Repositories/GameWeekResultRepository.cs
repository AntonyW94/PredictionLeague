using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class GameWeekResultRepository : IGameWeekResultRepository
{
    private readonly string _connectionString;

    public GameWeekResultRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task<IEnumerable<GameWeekResult>> GetByGameWeekIdAsync(int gameWeekId)
    {
        using var dbConnection = Connection;
        var sql = "SELECT gwr.* FROM [GameWeekResults] gwr WHERE gwr.[GameWeekId] = @GameWeekId;";
        return await dbConnection.QueryAsync<GameWeekResult>(sql, new { GameWeekId = gameWeekId });
    }

    public async Task UpsertAsync(GameWeekResult result)
    {
        using var dbConnection = Connection;
        const string sql = @"
                MERGE INTO [GameWeekResults] AS t
                USING (VALUES (@GameWeekId, @UserId)) AS s ([GameWeekId], [UserId])
                ON t.[GameWeekId] = s.[GameWeekId] AND t.[UserId] = s.[UserId]
                WHEN MATCHED THEN
                    UPDATE SET [TotalPoints] = @TotalPoints
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT ([GameWeekId], [UserId], [TotalPoints])
                    VALUES (@GameWeekId, @UserId, @TotalPoints);";
        await dbConnection.ExecuteAsync(sql, result);
    }
}