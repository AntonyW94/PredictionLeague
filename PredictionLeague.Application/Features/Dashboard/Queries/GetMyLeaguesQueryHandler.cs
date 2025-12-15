using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMyLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<MyLeagueDto>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH LeagueMemberCounts AS (
                SELECT 
                    [LeagueId],
                    COUNT([UserId]) AS [MemberCount]
                FROM 
                    [LeagueMembers]
                WHERE 
                    [Status] = @ApprovedStatus
                GROUP BY 
                    [LeagueId]
            ),

            LeagueUserPoints AS (
                SELECT
                    lm.UserId,
                    lm.LeagueId,
                    COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints]
                FROM 
		            [LeagueMembers] lm
                LEFT JOIN 
		            [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = lm.[LeagueId]
                WHERE 
		            lm.[Status] = @ApprovedStatus
                GROUP BY 
                    lm.[UserId],
		            lm.[LeagueId]
            ),

            RankedUsers AS (
                SELECT
                    [UserId],
                    [LeagueId],
                    RANK() OVER (PARTITION BY [LeagueId] ORDER BY [TotalPoints] DESC) AS [OverallRank]
                FROM [LeagueUserPoints]
            )

            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS [SeasonName],
                lm.[Status],
                ru.[OverallRank] AS [Rank],
                COALESCE(lmc.[MemberCount], 0) AS [MemberCount]
            FROM 
                [Leagues] l
            JOIN 
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN 
                [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            LEFT JOIN
                [LeagueMemberCounts] lmc ON l.[Id] = lmc.[LeagueId]
            LEFT JOIN
                [RankedUsers] ru ON ru.[UserId] = lm.[UserId] AND ru.[LeagueId] = l.[Id]
            WHERE 
                lm.[UserId] = @UserId
            ORDER BY 
                s.[StartDate] DESC, 
	            l.[Price] DESC,
                COALESCE(lmc.[MemberCount], 0) DESC,
                l.[Name];";

        return await _dbConnection.QueryAsync<MyLeagueDto>(sql, cancellationToken, new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }
}