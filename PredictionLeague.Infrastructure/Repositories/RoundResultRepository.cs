using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class RoundResultRepository : IRoundResultRepository
{
    private readonly string _connectionString;

    public RoundResultRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task<IEnumerable<RoundResult>> GetByRoundIdAsync(int roundId)
    {
        using var dbConnection = Connection;
        var sql = "SELECT gwr.* FROM [RoundResults] r WHERE r.[RoundId] = @RoundId;";
        return await dbConnection.QueryAsync<RoundResult>(sql, new { RoundId = roundId });
    }

    public async Task UpsertAsync(RoundResult result)
    {
        using var dbConnection = Connection;
        const string sql = @"
                MERGE INTO [RoundResults] AS t
                USING (VALUES (@RoundId, @UserId)) AS s ([RoundId], [UserId])
                ON t.[RoundId] = s.[RoundId] AND t.[UserId] = s.[UserId]
                WHEN MATCHED THEN
                    UPDATE SET [TotalPoints] = @TotalPoints
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT ([RoundId], [UserId], [TotalPoints])
                    VALUES (@RoundId, @UserId, @TotalPoints);";
        await dbConnection.ExecuteAsync(sql, result);
    }
}