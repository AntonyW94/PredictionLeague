using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leaderboards;
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

    public async Task<IEnumerable<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId)
    {
        const string sql = @"
            SELECT
                ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.PointsAwarded, 0)) DESC) AS Rank,
                u.FirstName + ' ' + u.LastName AS PlayerName,
                SUM(ISNULL(up.PointsAwarded, 0)) AS TotalPoints
            FROM
                LeagueMembers lm
            INNER JOIN
                AspNetUsers u ON lm.UserId = u.Id
            INNER JOIN
                Leagues l ON lm.LeagueId = l.Id
            LEFT JOIN
                UserPredictions up ON u.Id = up.UserId
            LEFT JOIN
                Matches m ON up.MatchId = m.Id
            LEFT JOIN
                Rounds r ON m.RoundId = r.Id AND r.SeasonId = l.SeasonId
            WHERE
                lm.LeagueId = @LeagueId 
            GROUP BY
                u.Id, 
                u.FirstName,
                u.LastName
            ORDER BY
                TotalPoints DESC,
                PlayerName ASC;";

        using var connection = Connection;
        return await connection.QueryAsync<LeaderboardEntryDto>(sql, new { LeagueId = leagueId });
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month, int year)
    {
        const string sql = @"
            SELECT
                ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.PointsAwarded, 0)) DESC) AS Rank,
                u.FirstName + ' ' + u.LastName AS PlayerName,
                SUM(ISNULL(up.PointsAwarded, 0)) AS TotalPoints
            FROM
                LeagueMembers lm
            INNER JOIN
                AspNetUsers u ON lm.UserId = u.Id
            INNER JOIN
                Leagues l ON lm.LeagueId = l.Id
            LEFT JOIN
                UserPredictions up ON u.Id = up.UserId
            LEFT JOIN
                Matches m ON up.MatchId = m.Id
            LEFT JOIN
                Rounds r ON m.RoundId = r.Id AND r.SeasonId = l.SeasonId  AND MONTH(r.StartDate) = @Month AND YEAR(r.StartDate) = @Year
            WHERE
                lm.LeagueId = @LeagueId
            GROUP BY
                u.Id,
                u.FirstName,
                u.LastName
            ORDER BY
                TotalPoints DESC, 
                PlayerName ASC;";

        using var connection = Connection;
        return await connection.QueryAsync<LeaderboardEntryDto>(sql, new { LeagueId = leagueId, Month = month, Year = year });
    }
}