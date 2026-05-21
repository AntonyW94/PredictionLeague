using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

public class GetMyLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    public async Task<IEnumerable<MyLeagueDto>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
        WITH MyLeagues AS (
            SELECT
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[Price],
		        l.[PrizeFundOverride],
                l.[IsFree],
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                s.[CompetitionType],
                s.[StartDateUtc] AS SeasonStartDateUtc,
                s.[NumberOfRounds],
                lm.[UserId],
                lm.[Status],
                lm.[IsArchivedByUser]
            FROM [LeagueMembers] lm
            JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN [Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
        ),

        ActiveRounds AS (
            SELECT 
                r.[SeasonId],
                r.[Id] AS RoundId,
                r.[RoundNumber],
                r.[StartDateUtc],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] WHERE [RoundId] = r.[Id] AND [Status] = @InProgressStatus) AS InProgressCount,
                (SELECT COUNT(*) FROM [Matches] WHERE [RoundId] = r.[Id] AND [Status] = @CompletedStatus) AS CompletedCount,
                ROW_NUMBER() OVER (
                    PARTITION BY r.[SeasonId] 
                        ORDER BY 
                           CASE 
                            WHEN r.[Status] = @InProgressStatus THEN 0 
                            WHEN r.[Status] = @CompletedStatus AND r.[CompletedDateUtc] > DATEADD(HOUR, -48, GETUTCDATE()) THEN 1
                            WHEN r.[Status] = @PublishedStatus THEN 2
                            ELSE 3 
                        END ASC,
                        r.[StartDateUtc] ASC
                ) as [PriorityRank]
            FROM [Rounds] r
            WHERE 
                r.[Status] <> @DraftStatus
                AND r.[SeasonId] IN (SELECT DISTINCT [SeasonId] FROM [MyLeagues])
        ),
        
        LeagueContext AS (
            SELECT
                l.[Id] AS LeagueId,
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) AS MemberCount,
                (SELECT ISNULL(SUM([Amount]), 0) FROM [Winnings] w JOIN [LeaguePrizeSettings] s ON w.[LeaguePrizeSettingId] = s.[Id] WHERE s.[LeagueId] = l.[Id]) AS TotalPaidOut,
                (SELECT ISNULL(SUM([Amount]), 0) FROM [Winnings] w JOIN [LeaguePrizeSettings] s ON w.[LeaguePrizeSettingId] = s.[Id] WHERE s.[LeagueId] = l.[Id] AND [UserId] = @UserId) AS UserWinnings,
                (SELECT COUNT(*) FROM (
                    SELECT lrr.[UserId], lrr.[BoostedPoints],
                        RANK() OVER (PARTITION BY lrr.[RoundId] ORDER BY lrr.[BoostedPoints] DESC) AS Rnk
                    FROM [LeagueRoundResults] lrr
                    INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                    WHERE lrr.[LeagueId] = l.[Id] AND r.[Status] = @CompletedStatus
                ) rw WHERE rw.[UserId] = @UserId AND rw.[Rnk] = 1 AND rw.[BoostedPoints] > 0) AS UserRoundsWon,
                (SELECT COUNT(*) FROM (
                    SELECT lrr.[UserId], SUM(lrr.[BoostedPoints]) AS MonthPoints,
                        RANK() OVER (PARTITION BY MONTH(r.[StartDateUtc]), YEAR(r.[StartDateUtc]) ORDER BY SUM(lrr.[BoostedPoints]) DESC) AS Rnk
                    FROM [LeagueRoundResults] lrr
                    INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                    WHERE lrr.[LeagueId] = l.[Id] AND r.[Status] = @CompletedStatus
                    GROUP BY MONTH(r.[StartDateUtc]), YEAR(r.[StartDateUtc]), lrr.[UserId]
                ) mw WHERE mw.[UserId] = @UserId AND mw.[Rnk] = 1 AND mw.[MonthPoints] > 0) AS UserMonthsWon
            FROM [Leagues] l
            WHERE l.[Id] IN (SELECT [LeagueId] FROM [MyLeagues])
        ),

        ActiveRoundMonthlyRanks AS (
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                CAST(RANK() OVER (
                    PARTITION BY lm.[LeagueId]
                    ORDER BY ISNULL(SUM(CASE WHEN r.[Id] IS NOT NULL THEN lrr.[BoostedPoints] ELSE 0 END), 0) DESC
                ) AS INT) AS ActiveMonthRank,
                CASE
                    WHEN NOT EXISTS (
                        SELECT 1 FROM [Rounds] r2
                        WHERE r2.[SeasonId] = lg.[SeasonId]
                        AND MONTH(r2.[StartDateUtc]) = MONTH(ar.[StartDateUtc])
                        AND YEAR(r2.[StartDateUtc]) = YEAR(ar.[StartDateUtc])
                        AND r2.[Id] <> ar.[RoundId]
                        AND r2.[Status] IN (@InProgressStatus, @CompletedStatus)
                    )
                    THEN NULL
                    ELSE CAST(RANK() OVER (
                        PARTITION BY lm.[LeagueId]
                        ORDER BY ISNULL(SUM(CASE WHEN r.[Id] <> ar.[RoundId] THEN lrr.[BoostedPoints] ELSE 0 END), 0) DESC
                    ) AS INT)
                END AS PreRoundMonthRank
            FROM [LeagueMembers] lm
            JOIN [Leagues] lg ON lm.[LeagueId] = lg.[Id]
            JOIN [ActiveRounds] ar ON lg.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
            LEFT JOIN [LeagueRoundResults] lrr ON lm.[LeagueId] = lrr.[LeagueId] AND lm.[UserId] = lrr.[UserId]
            LEFT JOIN [Rounds] r ON lrr.[RoundId] = r.[Id]
                AND MONTH(r.[StartDateUtc]) = MONTH(ar.[StartDateUtc])
                AND YEAR(r.[StartDateUtc]) = YEAR(ar.[StartDateUtc])
            WHERE lm.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
                AND lm.[Status] = @ApprovedStatus
            GROUP BY lm.[LeagueId], lm.[UserId], ar.[RoundId], lg.[SeasonId], ar.[StartDateUtc]
        )

        SELECT
            l.[LeagueId] AS Id,
            l.[LeagueName] AS Name,
            l.[SeasonName],
            l.[CompetitionType],
            l.[SeasonStartDateUtc],

            CASE WHEN ar.[RoundId] IS NOT NULL THEN 'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10)) ELSE NULL END AS CurrentRound,
            CASE
                WHEN ar.[RoundId] IS NULL THEN NULL
                WHEN l.[CompetitionType] = 1 THEN 'Exact Scores'
                ELSE DATENAME(MONTH, ar.[StartDateUtc])
            END AS CurrentMonth,
            ar.[StartDateUtc] AS RoundStartDateUtc,
            ISNULL(lc.[MemberCount], 0) AS MemberCount,

            ISNULL(stats.[OverallRank], 1) AS Rank,
            armr.[ActiveMonthRank] AS MonthRank,
            CASE 
                WHEN ar.[Status] = @PublishedStatus THEN 1                    
                ELSE stats.[LiveRoundRank]
            END AS RoundRank,

            stats.[SnapshotOverallRank] AS PreRoundOverallRank,
            armr.[PreRoundMonthRank] AS PreRoundMonthRank,
            CASE 
                WHEN ar.[Status] = @PublishedStatus THEN 1                    
                ELSE stats.[StableRoundRank]
            END AS StableRoundRank,

            ar.[Status] AS RoundStatus,
            ISNULL(ar.[InProgressCount], 0) AS InProgressCount,
            ISNULL(ar.[CompletedCount], 0) AS CompletedCount,

            lc.[UserWinnings] AS PrizeMoneyWon,
            (COALESCE(l.[PrizeFundOverride], l.[Price] * lc.[MemberCount]) - lc.[TotalPaidOut]) AS PrizeMoneyRemaining,
            COALESCE(l.[PrizeFundOverride], l.[Price] * lc.[MemberCount]) AS TotalPrizeFund,
            l.[Price] AS EntryFee,
            l.[IsFree],

            ISNULL(lc.[UserRoundsWon], 0) AS RoundsWon,
            ISNULL(lc.[UserMonthsWon], 0) AS MonthsWon,

            CAST(CASE
                WHEN (SELECT COUNT(*) FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] = @CompletedStatus) >= l.[NumberOfRounds]
                THEN 1
                ELSE 0
            END AS bit) AS IsFinished,
            l.[IsArchivedByUser]

        FROM [MyLeagues] l
        LEFT JOIN [LeagueMemberStats] stats ON l.[LeagueId] = stats.[LeagueId] AND l.[UserId] = stats.[UserId]
        LEFT JOIN [ActiveRounds] ar ON l.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
        LEFT JOIN [LeagueContext] lc ON l.[LeagueId] = lc.[LeagueId]
        LEFT JOIN [ActiveRoundMonthlyRanks] armr ON l.[LeagueId] = armr.[LeagueId] AND l.[UserId] = armr.[UserId]

        ORDER BY
            CASE WHEN ar.[Status] = @InProgressStatus THEN 0 ELSE 1 END ASC,
            l.[SeasonStartDateUtc] ASC,
            l.[Price] DESC,
            l.[LeagueName]";

        return await dbConnection.QueryAsync<MyLeagueDto>(
            sql,
            cancellationToken,
            new
            {
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                DraftStatus = nameof(RoundStatus.Draft),
                PublishedStatus = nameof(RoundStatus.Published),
                InProgressStatus = nameof(RoundStatus.InProgress),
                CompletedStatus = nameof(RoundStatus.Completed)
            });
    }
}