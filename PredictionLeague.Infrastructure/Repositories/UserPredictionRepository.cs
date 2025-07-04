using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class UserPredictionRepository : IUserPredictionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public UserPredictionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<UserPrediction>> GetByMatchIdAsync(int matchId)
    {
        const string sql = "SELECT up.* FROM [UserPredictions] up WHERE up.[MatchId] = @MatchId;";

        using var dbConnection = Connection;
        return await dbConnection.QueryAsync<UserPrediction>(sql, new { MatchId = matchId });
    }

    public async Task<IEnumerable<UserPrediction>> GetByUserIdAndRoundIdAsync(string userId, int roundId)
    {
        const string sql = @"
            SELECT up.* FROM [UserPredictions] up
            INNER JOIN [Matches] m ON up.[MatchId] = m.[Id]
            WHERE up.[UserId] = @UserId AND m.[RoundId] = @RoundId;";

        using var connection = Connection;
        return await connection.QueryAsync<UserPrediction>(sql, new { UserId = userId, RoundId = roundId });
    }

    public async Task UpsertAsync(UserPrediction prediction)
    {
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

        using var dbConnection = Connection;
        await dbConnection.ExecuteAsync(sql, prediction);
    }

    public async Task UpdatePointsAsync(int predictionId, int points)
    {
        const string sql = "UPDATE [UserPredictions] SET [PointsAwarded] = @Points WHERE [Id] = @PredictionId;";

        using var connection = Connection;
        await connection.ExecuteAsync(sql, new { Points = points, PredictionId = predictionId });
    }
}