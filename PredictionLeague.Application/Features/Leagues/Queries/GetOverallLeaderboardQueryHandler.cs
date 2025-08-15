using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetOverallLeaderboardQueryHandler : IRequestHandler<GetOverallLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetOverallLeaderboardQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetOverallLeaderboardQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                RANK() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS [Rank],
                au.[FirstName] + ' ' + LEFT(au.[LastName], 1) AS [PlayerName],
                SUM(ISNULL(up.[PointsAwarded], 0)) AS [TotalPoints],
                au.[Id] AS [UserId]
            FROM 
                [LeagueMembers] AS lm
            JOIN 
                [AspNetUsers] AS au ON lm.[UserId] = au.[Id]
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
                AND lm.[Status] = @ApprovedStatus
            GROUP BY
                au.[UserName], au.[FirstName], au.[LastName], au.[Id]
            ORDER BY
                [Rank], [PlayerName];";

        return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            }
        );
    }
}
