using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetExactScoresLeaderboardQueryHandler(IApplicationReadDbConnection connection) : IRequestHandler<GetExactScoresLeaderboardQuery, ExactScoresLeaderboardDto>
{
    public async Task<ExactScoresLeaderboardDto> Handle(GetExactScoresLeaderboardQuery request, CancellationToken cancellationToken)
    {
        const string entriesSql = @"
            SELECT
	            RANK() OVER (ORDER BY COALESCE(SUM(rr.[ExactScoreCount]), 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName],
  	            COALESCE(SUM(rr.[ExactScoreCount]), 0) AS [ExactScoresCount],
	            u.[Id] AS [UserId]
            FROM 
	            [LeagueMembers] lm
            JOIN 
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            LEFT JOIN		
	            [RoundResults] rr ON lm.[UserId] = rr.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId 
                AND lm.[Status] = @ApprovedStatus
            GROUP BY 
                u.[FirstName],
                u.[LastName],
                u.[Id]
            ORDER BY
                [ExactScoresCount] DESC, 
                [PlayerName]";

        var leaderboardEntries = await connection.QueryAsync<ExactScoresLeaderboardEntryDto>(entriesSql, cancellationToken, new { request.LeagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
      
        var leaderboard = new ExactScoresLeaderboardDto
        {
            Entries = leaderboardEntries.ToList()
        };

        return leaderboard;
    }
}
