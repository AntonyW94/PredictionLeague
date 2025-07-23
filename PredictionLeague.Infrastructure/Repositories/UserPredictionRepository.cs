using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
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

    #region Create

    public Task UpsertBatchAsync(IEnumerable<UserPrediction> predictions, CancellationToken cancellationToken)
    {
        const string sql = @"
        MERGE INTO [dbo].[UserPredictions] AS target
        USING (SELECT @UserId AS UserId, @MatchId AS MatchId) AS source
        ON (target.[UserId] = source.[UserId] AND target.[MatchId] = source.[MatchId])
        WHEN MATCHED THEN
            UPDATE SET 
                [PredictedHomeScore] = @PredictedHomeScore,
                [PredictedAwayScore] = @PredictedAwayScore,
                [UpdatedAt] = @UpdatedAt
        WHEN NOT MATCHED THEN
            INSERT ([MatchId], [UserId], [PredictedHomeScore], [PredictedAwayScore], [PointsAwarded], [CreatedAt], [UpdatedAt])
            VALUES (@MatchId, @UserId, @PredictedHomeScore, @PredictedAwayScore, @PointsAwarded, @CreatedAt, @UpdatedAt);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: predictions,
            cancellationToken: cancellationToken
        );

        return Connection.ExecuteAsync(command);
    }

    #endregion
}