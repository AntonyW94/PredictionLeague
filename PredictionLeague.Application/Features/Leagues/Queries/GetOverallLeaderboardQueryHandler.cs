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
                RANK() OVER (ORDER BY COALESCE(SUM(lrr.BoostedPoints), 0) DESC) AS [Rank],
                au.[FirstName] + ' ' + LEFT(au.[LastName], 1) AS [PlayerName],
                COALESCE(SUM(lrr.BoostedPoints), 0) AS [TotalPoints],
                au.[Id] AS [UserId]
            FROM 
	            [LeagueMembers] AS lm
            JOIN 
	            [AspNetUsers] AS au ON lm.[UserId] = au.[Id]
            LEFT JOIN 
	            [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId
            WHERE 
	            lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus
            GROUP BY 
	            au.[FirstName], au.[LastName], au.[Id]
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
