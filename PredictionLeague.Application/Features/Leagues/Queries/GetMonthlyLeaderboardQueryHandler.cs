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
            SELECT
                ROW_NUMBER() OVER (ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS Rank,
                u.[FirstName] + ' ' + u.[LastName] AS Username,
                SUM(ISNULL(up.[PointsAwarded], 0)) AS Points
            FROM [LeagueMembers] lm
            JOIN [AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN [UserPredictions] up ON u.[Id] = up.[UserId]
            LEFT JOIN [Matches] m ON up.[MatchId] = m.[Id]
            LEFT JOIN [Rounds] r ON m.[RoundId] = r.[Id]
            LEFT JOIN [Seasons] s ON r.[SeasonId] = s.[Id]
            WHERE lm.[LeagueId] = @LeagueId
              AND lm.[Status] = @Status
              AND (@Month = 0 OR MONTH(m.[MatchDateTime]) = @Month)
              AND s.[Id] = (SELECT SeasonId FROM Leagues WHERE Id = @LeagueId)
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
                request.Month,
                Status = nameof(LeagueMemberStatus.Approved)
            }
        );
    }
}