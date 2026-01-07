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
                RANK() OVER (ORDER BY COALESCE(SUM(lrr.[BoostedPoints]), 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName],
                COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints],
                u.[Id] AS [UserId],
                stats.[SnapshotOverallRank] AS [SnapshotRank],
                CASE WHEN EXISTS (
                    SELECT 1 
                    FROM [Rounds] r 
                    JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId] 
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @InProgressStatus
                ) THEN 1 ELSE 0 END AS [IsRoundInProgress]
            
            FROM 
	            [LeagueMembers] lm
            
            JOIN 
	            [AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN 
	            [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId
            LEFT JOIN 
                [LeagueMemberStats] stats ON lm.[LeagueId] = stats.[LeagueId] AND lm.[UserId] = stats.[UserId]
           
            WHERE 
	            lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus

            GROUP BY 
	            u.[FirstName], 
                u.[LastName], 
                u.[Id],
                stats.[SnapshotOverallRank]

            ORDER BY 
	            [Rank], 
                [PlayerName];";

        return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(RoundStatus.InProgress)
            }
        );
    }
}
