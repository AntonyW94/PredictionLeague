using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetMonthlyLeaderboardQueryHandler : IRequestHandler<GetMonthlyLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMonthlyLeaderboardQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetMonthlyLeaderboardQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH MonthlyRounds AS (
                SELECT [Id]
                FROM [Rounds]
                WHERE MONTH([StartDate]) = @Month
                    AND [SeasonId] = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId)
            )

            SELECT
                RANK() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS Rank,
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                SUM(ISNULL(up.[PointsAwarded], 0)) AS TotalPoints,
                u.[Id] AS [UserId]
            FROM 
                [LeagueMembers] lm
            JOIN 
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            JOIN 
                [Leagues] AS l ON lm.[LeagueId] = l.[Id]
            JOIN 
                [Seasons] AS s ON l.[SeasonId] = s.[Id]
            JOIN 
                [Rounds] AS r ON s.[Id] = r.[SeasonId]
            JOIN 
                [Matches] AS m ON r.[Id] = m.[RoundId]
            LEFT JOIN 
                [UserPredictions] AS up ON m.[Id] = up.[MatchId] AND lm.[UserId] = up.[UserId]
            WHERE 
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @Status
                AND m.[RoundId] IN (SELECT [Id] FROM [MonthlyRounds])
            GROUP BY
                lm.[UserId],
                u.[FirstName],
                u.[LastName],
                u.[Id]
            ORDER BY
                TotalPoints DESC,
                PlayerName ASC;";

        return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                request.Month,
                Status = nameof(LeagueMemberStatus.Approved)
            }
        );
    }
}