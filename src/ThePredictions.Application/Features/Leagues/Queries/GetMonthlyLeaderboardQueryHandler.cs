using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetMonthlyLeaderboardQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetMonthlyLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetMonthlyLeaderboardQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);
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
                    )
                    AND (
                        SELECT COUNT(*)
                        FROM [Rounds] r2
                        WHERE r2.[Id] IN (SELECT [Id] FROM [MonthlyRounds])
                        AND r2.[Status] IN (@InProgressStatus, @CompletedStatus)
                    ) > 1
                    THEN stats.[SnapshotMonthRank]
                    ELSE NULL
                END AS [SnapshotRank],

                CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Rounds] r
                    JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @InProgressStatus
                ) THEN 1 ELSE 0 END AS bit) AS [IsRoundInProgress]
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

        var entries = await dbConnection.QueryAsync<MonthlyLeaderboardQueryResult>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                request.Month,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(RoundStatus.InProgress),
                CompletedStatus = nameof(RoundStatus.Completed)
            }
        );

        return entries.Select(e => new LeaderboardEntryDto
        {
            Rank = e.Rank,
            PlayerName = e.PlayerName,
            TotalPoints = e.TotalPoints,
            UserId = e.UserId,
            SnapshotRank = e.SnapshotRank,
            IsRoundInProgress = e.IsRoundInProgress
        });
    }

    // SnapshotRank is [LeagueMemberStats].[SnapshotMonthRank], an int column, so it must be int? here
    // even though LeaderboardEntryDto exposes it as long? - Dapper's constructor match is exact per column.
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record MonthlyLeaderboardQueryResult(
        long Rank,
        string PlayerName,
        int? TotalPoints,
        string UserId,
        int? SnapshotRank,
        bool IsRoundInProgress);
}