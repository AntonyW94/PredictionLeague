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
                SELECT 
                    [Id]
                FROM 
                    [Rounds]
                WHERE
                    MONTH ([StartDate]) = @Month
                    AND [SeasonId] = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId)
            )

            SELECT
                RANK() OVER (ORDER BY COALESCE(SUM(lrr.[BoostedPoints]), 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints],
                u.[Id] AS [UserId]
            FROM 
                [LeagueMembers] lm
            JOIN 
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN 
	            [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId AND lrr.[RoundId] IN (SELECT [Id] FROM [MonthlyRounds])
            WHERE 
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus
            GROUP BY
                u.[FirstName],
                u.[LastName],
                u.[Id]
            ORDER BY
                [Rank] ASC,
                [PlayerName] ASC;";

        return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                request.Month,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            }
        );
    }
}