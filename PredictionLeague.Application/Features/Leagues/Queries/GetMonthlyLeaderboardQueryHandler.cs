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
                    MONTH ([StartDateUtc]) = @Month
                    AND [SeasonId] = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId)
            )

            SELECT
                RANK() OVER (ORDER BY COALESCE(SUM(lrr.[BoostedPoints]), 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints],
                u.[Id] AS [UserId],

                CASE 
                    WHEN EXISTS (
                        SELECT 1 
                        FROM [Rounds] r
                        WHERE r.[Id] IN (SELECT [Id] FROM [MonthlyRounds])
                        AND r.[Status] = @InProgressStatus                
                    ) THEN stats.[SnapshotMonthRank] 
                    ELSE NULL 
                END AS [SnapshotRank],

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
	            [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId AND lrr.[RoundId] IN (SELECT [Id] FROM [MonthlyRounds])
            LEFT JOIN 
                [LeagueMemberStats] stats ON lm.[LeagueId] = stats.[LeagueId] AND lm.[UserId] = stats.[UserId]
            WHERE 
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus
            GROUP BY
                u.[FirstName],
                u.[LastName],
                u.[Id],
                stats.[SnapshotMonthRank]
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
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(RoundStatus.InProgress),
            }
        );
    }
}