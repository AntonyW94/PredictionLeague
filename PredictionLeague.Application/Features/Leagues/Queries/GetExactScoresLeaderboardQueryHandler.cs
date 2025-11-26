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
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                u.[Id] AS [UserId],
                SUM(rr.ExactScoreCount) AS ExactScoresCount
            FROM 
                [RoundResults] rr
            JOIN 
                [Rounds] AS r ON r.[Id] = rr.[RoundId]
            JOIN 
                [Leagues] AS l ON l.[SeasonId] = r.[SeasonId]
            JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id] AND lm.[UserId] = rr.[UserId] AND lm.[Status] = @ApprovedStatus
            JOIN 
                [AspNetUsers] u ON u.[Id] = rr.[UserId]
            WHERE
                l.[Id] = @LeagueId 
            GROUP BY 
                u.[FirstName],
                u.[LastName],
                u.[Id]
            ORDER BY
                ExactScoresCount DESC, 
                PlayerName";

        var leaderboardEntries = await connection.QueryAsync<ExactScoresLeaderboardEntryDto>(entriesSql, cancellationToken, new { request.LeagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
      
        var leaderboard = new ExactScoresLeaderboardDto
        {
            Entries = leaderboardEntries.ToList()
        };

        var rank = 1;
        for (var i = 0; i < leaderboard.Entries.Count; i++)
        {
            if (i > 0 && leaderboard.Entries[i].ExactScoresCount < leaderboard.Entries[i - 1].ExactScoresCount)
            {
                rank = i + 1;
            }
            leaderboard.Entries[i].Rank = rank;
        }

        return leaderboard;
    }
}
