using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class UserPredictionRepository : IUserPredictionRepository
{
    private readonly string _connectionString;

    public UserPredictionRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task<IEnumerable<UserPrediction>> GetByMatchIdAsync(int matchId)
    {
        using var dbConnection = Connection;
        const string sql = "SELECT up.* FROM [UserPredictions] up WHERE up.[MatchId] = @MatchId;";
      
        return await dbConnection.QueryAsync<UserPrediction>(sql, new { MatchId = matchId });
    }

    public async Task<IEnumerable<UserPrediction>> GetByUserIdAndGameWeekIdAsync(string userId, int gameWeekId)
    {
        using var dbConnection = Connection;
        const string sql = @"
                SELECT up.* FROM [UserPredictions] up
                JOIN [Matches] m ON up.[MatchId] = m.[Id]
                WHERE up.[UserId] = @UserId AND m.[GameWeekId] = @GameWeekId;";
     
        return await dbConnection.QueryAsync<UserPrediction>(sql, new { UserId = userId, GameWeekId = gameWeekId });
    }

    public async Task UpsertAsync(UserPrediction prediction)
    {
        using var dbConnection = Connection;
        // This MERGE statement is the core of the Upsert logic.
        // It checks if a record exists for the match and user.
        // If it exists (MATCHED), it UPDATES it.
        // If it doesn't (NOT MATCHED), it INSERTS a new one.
        const string sql = @"
                MERGE INTO [UserPredictions] AS t
                USING (VALUES (@MatchId, @UserId)) AS s ([MatchId], [UserId])
                ON t.[MatchId] = s.[MatchId] AND t.[UserId] = s.[UserId]
                WHEN MATCHED THEN
                    UPDATE SET
                        [PredictedHomeScore] = @PredictedHomeScore,
                        [PredictedAwayScore] = @PredictedAwayScore,
                        [UpdatedAt] = GETDATE()
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT ([MatchId], [UserId], [PredictedHomeScore], [PredictedAwayScore], [PointsAwarded], [CreatedAt], [UpdatedAt])
                    VALUES (@MatchId, @UserId, @PredictedHomeScore, @PredictedAwayScore, NULL, GETDATE(), GETDATE());";
      
        await dbConnection.ExecuteAsync(sql, prediction);
    }
}