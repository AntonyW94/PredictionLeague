using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leaderboards;
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

    #region Read

    public async Task<IEnumerable<UserPrediction>> FetchByUserIdAndRoundIdAsync(string userId, int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT up.* FROM [UserPredictions] up
            INNER JOIN [Matches] m ON up.[MatchId] = m.[Id]
            WHERE up.[UserId] = @UserId AND m.[RoundId] = @RoundId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { UserId = userId, RoundId = roundId },
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<UserPrediction>(command);
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> FetchOverallLeaderboardAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS [Rank],
                u.[FirstName] + ' ' + u.[LastName] AS [PlayerName],
                SUM(ISNULL(up.[PointsAwarded], 0)) AS [TotalPoints]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            INNER JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            LEFT JOIN
                [UserPredictions] up ON u.[Id] = up.[UserId]
            LEFT JOIN
                [Matches] m ON up.[MatchId] = m.[Id]
            LEFT JOIN
                [Rounds] r ON m.[RoundId] = r.[Id] AND r.[SeasonId] = l.[SeasonId]
            WHERE
                lm.[LeagueId] = @LeagueId AND lm.[Status] = 'Approved'
            GROUP BY
                u.[Id], 
                u.[FirstName],
                u.[LastName]
            ORDER BY
                [TotalPoints] DESC,
                [PlayerName] ASC;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId },
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<LeaderboardEntryDto>(command);
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> FetchMonthlyLeaderboardAsync(int leagueId, int month, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS [Rank],
                u.[FirstName] + ' ' + u.[LastName] AS [PlayerName],
                SUM(ISNULL(up.[PointsAwarded], 0)) AS [TotalPoints]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            INNER JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            LEFT JOIN
                [UserPredictions] up ON u.[Id] = up.[UserId]
            LEFT JOIN
                [Matches] m ON up.[MatchId] = m.[Id]
            LEFT JOIN
                [Rounds] r ON m.[RoundId] = r.[Id] AND r.[SeasonId] = l.[SeasonId] AND MONTH(r.[StartDate]) = @Month
            WHERE
                lm.[LeagueId] = @LeagueId AND lm.[Status] = 'Approved'
            GROUP BY
                u.[Id],
                u.[FirstName],
                u.[LastName]
            ORDER BY
                [TotalPoints] DESC, 
                [PlayerName] ASC;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId, Month = month },
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<LeaderboardEntryDto>(command);
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> FetchRoundLeaderboardAsync(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT
            ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS [Rank],
            u.[FirstName] + ' ' + u.[LastName] AS [PlayerName],
            SUM(ISNULL(up.[PointsAwarded], 0)) AS [TotalPoints]
        FROM
            [LeagueMembers] lm
        INNER JOIN
            [AspNetUsers] u ON lm.[UserId] = u.[Id]
        INNER JOIN
            [Leagues] l ON lm.[LeagueId] = l.[Id]
        LEFT JOIN
            [UserPredictions] up ON u.[Id] = up.[UserId]
        LEFT JOIN
            [Matches] m ON up.[MatchId] = m.[Id] AND m.[RoundId] = @RoundId
        WHERE
            lm.[LeagueId] = @LeagueId AND lm.[Status] = 'Approved'
        GROUP BY
            u.[Id],
            u.[FirstName],
            u.[LastName]
        ORDER BY
            [TotalPoints] DESC, 
            [PlayerName] ASC;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId, RoundId = roundId },
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<LeaderboardEntryDto>(command);
    }

    #endregion
}