using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetRoundLeaderboardQueryHandler : IRequestHandler<GetRoundLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetRoundLeaderboardQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetRoundLeaderboardQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS Rank,
                u.[FirstName] + ' ' + u.[LastName] AS Username,
                SUM(ISNULL(up.[PointsAwarded], 0)) AS Points
            FROM [dbo].[LeagueMembers] lm
            JOIN [dbo].[AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN [dbo].[UserPredictions] up ON u.[Id] = up.[UserId]
            LEFT JOIN [dbo].[Matches] m ON up.[MatchId] = m.[Id]
            WHERE lm.[LeagueId] = @LeagueId
            AND lm.[Status] = @Status
            AND m.[RoundId] = @RoundId
            GROUP BY
                lm.[UserId],
                u.[FirstName],
                u.[LastName]
            ORDER BY
                Points DESC,
                Username ASC;";

        return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
            sql,
            cancellationToken,
            new
            { 
                request.LeagueId, 
                request.RoundId,
                Status = nameof(LeagueMemberStatus.Approved)
            }
        );
    }
}
