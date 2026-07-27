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
        // Runs under READ UNCOMMITTED. This dashboard tile is a high-frequency read that was blocking for
        // several seconds behind the results/stats write path, because the database does not have
        // READ_COMMITTED_SNAPSHOT enabled and it cannot be turned on for this managed instance. The tile is
        // a live view that auto-refreshes, so a transient dirty read self-corrects on the next poll, whereas
        // the multi-second lock wait was user-visible. The isolation level is reset at the end of the batch
        // so it cannot leak to other reads that reuse the pooled connection.
        const string sql = @"
        SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

        WITH MyLeagues AS (
            SELECT
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[Price],
		        l.[PrizeFundOverride],
                l.[IsFree],
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                c.[Type] AS CompetitionType,
                s.[StartDateUtc] AS SeasonStartDateUtc,
                l.[EntryDeadlineUtc],
                s.[NumberOfRounds],
                lm.[UserId],
                lm.[Status],
                lm.[IsArchivedByUser]
            FROM [LeagueMembers] lm
            JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN [Competitions] c ON s.[CompetitionId] = c.[Id]
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
                        r.[RoundNumber] ASC
                ) as [PriorityRank]
            FROM [Rounds] r
            WHERE
                r.[Status] <> @DraftStatus
                AND r.[SeasonId] IN (SELECT DISTINCT [SeasonId] FROM [MyLeagues])
        ),

        RoundStages AS (
            SELECT
                r.[Id] AS RoundId,
                r.[SeasonId],
                r.[Status],
                CASE WHEN trm.[Stages] LIKE '%Group%' THEN 'Group Stage' ELSE 'Knockout Stage' END AS StageName
            FROM [Rounds] r
            JOIN [TournamentRoundMappings] trm ON trm.[SeasonId] = r.[SeasonId] AND trm.[RoundNumber] = r.[RoundNumber]
            WHERE r.[SeasonId] IN (SELECT DISTINCT [SeasonId] FROM [MyLeagues])
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
        ),

        ActiveStageRanks AS (
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                ars.[StageName],
                CAST(RANK() OVER (
                    PARTITION BY lm.[LeagueId]
                    ORDER BY ISNULL(SUM(CASE WHEN rs.[RoundId] IS NOT NULL THEN lrr.[BoostedPoints] ELSE 0 END), 0) DESC
                ) AS INT) AS StageRank,
                CASE
                    WHEN NOT EXISTS (
                        SELECT 1 FROM [RoundStages] rs2
                        WHERE rs2.[SeasonId] = lg.[SeasonId]
                        AND rs2.[StageName] = ars.[StageName]
                        AND rs2.[RoundId] <> ar.[RoundId]
                        AND rs2.[Status] IN (@InProgressStatus, @CompletedStatus)
                    )
                    THEN NULL
                    ELSE CAST(RANK() OVER (
                        PARTITION BY lm.[LeagueId]
                        ORDER BY ISNULL(SUM(CASE WHEN rs.[RoundId] <> ar.[RoundId] THEN lrr.[BoostedPoints] ELSE 0 END), 0) DESC
                    ) AS INT)
                END AS PreRoundStageRank
            FROM [LeagueMembers] lm
            JOIN [Leagues] lg ON lm.[LeagueId] = lg.[Id]
            JOIN [ActiveRounds] ar ON lg.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
            JOIN [RoundStages] ars ON ars.[RoundId] = ar.[RoundId]
            LEFT JOIN [LeagueRoundResults] lrr ON lm.[LeagueId] = lrr.[LeagueId] AND lm.[UserId] = lrr.[UserId]
            LEFT JOIN [RoundStages] rs ON lrr.[RoundId] = rs.[RoundId] AND rs.[StageName] = ars.[StageName]
            WHERE lm.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
                AND lm.[Status] = @ApprovedStatus
            GROUP BY lm.[LeagueId], lm.[UserId], ar.[RoundId], lg.[SeasonId], ars.[StageName]
        ),

        ActiveOverallRanks AS (
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                CAST(RANK() OVER (
                    PARTITION BY lm.[LeagueId]
                    ORDER BY ISNULL(SUM(lrr.[BoostedPoints]), 0) DESC
                ) AS INT) AS OverallRank,
                CASE
                    WHEN NOT EXISTS (
                        SELECT 1 FROM [Rounds] r2
                        WHERE r2.[SeasonId] = lg.[SeasonId]
                        AND r2.[RoundNumber] < ar.[RoundNumber]
                        AND r2.[Status] IN (@InProgressStatus, @CompletedStatus)
                    )
                    THEN NULL
                    ELSE CAST(RANK() OVER (
                        PARTITION BY lm.[LeagueId]
                        ORDER BY ISNULL(SUM(CASE WHEN rr.[RoundNumber] < ar.[RoundNumber] THEN lrr.[BoostedPoints] ELSE 0 END), 0) DESC
                    ) AS INT)
                END AS PreRoundOverallRank
            FROM [LeagueMembers] lm
            JOIN [Leagues] lg ON lm.[LeagueId] = lg.[Id]
            JOIN [ActiveRounds] ar ON lg.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
            LEFT JOIN [LeagueRoundResults] lrr ON lm.[LeagueId] = lrr.[LeagueId] AND lm.[UserId] = lrr.[UserId]
            LEFT JOIN [Rounds] rr ON lrr.[RoundId] = rr.[Id]
            WHERE lm.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
                AND lm.[Status] = @ApprovedStatus
            GROUP BY lm.[LeagueId], lm.[UserId], lg.[SeasonId], ar.[RoundNumber]
        ),

        ActiveRoundRanks AS (
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                CAST(RANK() OVER (
                    PARTITION BY lm.[LeagueId]
                    ORDER BY ISNULL(SUM(lrr.[BoostedPoints]), 0) DESC
                ) AS INT) AS LiveRoundRank
            FROM [LeagueMembers] lm
            JOIN [Leagues] lg ON lm.[LeagueId] = lg.[Id]
            JOIN [ActiveRounds] ar ON lg.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
            LEFT JOIN [LeagueRoundResults] lrr ON lm.[LeagueId] = lrr.[LeagueId] AND lm.[UserId] = lrr.[UserId] AND lrr.[RoundId] = ar.[RoundId]
            WHERE lm.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
                AND lm.[Status] = @ApprovedStatus
            GROUP BY lm.[LeagueId], lm.[UserId]
        ),

        ActiveExactScoresRanks AS (
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                CAST(RANK() OVER (
                    PARTITION BY lm.[LeagueId]
                    ORDER BY ISNULL(SUM(CASE WHEN r.[Id] IS NOT NULL THEN rr.[ExactScoreCount] ELSE 0 END), 0) DESC
                ) AS INT) AS ExactScoresRank,
                CASE
                    WHEN NOT EXISTS (
                        SELECT 1 FROM [Rounds] r2
                        WHERE r2.[SeasonId] = lg.[SeasonId]
                        AND r2.[RoundNumber] < ar.[RoundNumber]
                        AND r2.[Status] IN (@InProgressStatus, @CompletedStatus)
                    )
                    THEN NULL
                    ELSE CAST(RANK() OVER (
                        PARTITION BY lm.[LeagueId]
                        ORDER BY ISNULL(SUM(CASE WHEN r.[Id] IS NOT NULL AND r.[RoundNumber] < ar.[RoundNumber] THEN rr.[ExactScoreCount] ELSE 0 END), 0) DESC
                    ) AS INT)
                END AS PreRoundExactScoresRank
            FROM [LeagueMembers] lm
            JOIN [Leagues] lg ON lm.[LeagueId] = lg.[Id]
            JOIN [ActiveRounds] ar ON lg.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
            LEFT JOIN [RoundResults] rr ON rr.[UserId] = lm.[UserId]
            LEFT JOIN [Rounds] r ON r.[Id] = rr.[RoundId] AND r.[SeasonId] = lg.[SeasonId]
            WHERE lm.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
                AND lm.[Status] = @ApprovedStatus
            GROUP BY lm.[LeagueId], lm.[UserId], lg.[SeasonId], ar.[RoundNumber]
        )

        SELECT
            l.[LeagueId] AS Id,
            l.[LeagueName] AS Name,
            l.[SeasonName],
            l.[CompetitionType],
            l.[SeasonStartDateUtc],
            l.[EntryDeadlineUtc],

            CASE WHEN ar.[RoundId] IS NOT NULL THEN 'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10)) ELSE NULL END AS CurrentRound,
            CASE
                WHEN ar.[RoundId] IS NULL THEN NULL
                WHEN l.[CompetitionType] = 1 THEN 'Exact Scores'
                ELSE DATENAME(MONTH, ar.[StartDateUtc])
            END AS CurrentMonth,
            ar.[StartDateUtc] AS RoundStartDateUtc,
            ISNULL(lc.[MemberCount], 0) AS MemberCount,

            aor.[OverallRank] AS Rank,
            CASE WHEN l.[CompetitionType] = 1 THEN aes.[ExactScoresRank] ELSE armr.[ActiveMonthRank] END AS MonthRank,
            CASE
                WHEN ar.[Status] = @PublishedStatus THEN 1
                ELSE arr.[LiveRoundRank]
            END AS RoundRank,

            aor.[PreRoundOverallRank] AS PreRoundOverallRank,
            CASE WHEN l.[CompetitionType] = 1 THEN aes.[PreRoundExactScoresRank] ELSE armr.[PreRoundMonthRank] END AS PreRoundMonthRank,
            CASE 
                WHEN ar.[Status] = @PublishedStatus THEN 1                    
                ELSE stats.[StableRoundRank]
            END AS StableRoundRank,

            ar.[Status] AS RoundStatus,
            ISNULL(ar.[InProgressCount], 0) AS InProgressCount,
            ISNULL(ar.[CompletedCount], 0) AS CompletedCount,

            lc.[UserWinnings] AS PrizeMoneyWon,
            (l.[Price] * lc.[MemberCount] + ISNULL(l.[PrizeFundOverride], 0) - lc.[TotalPaidOut]) AS PrizeMoneyRemaining,
            (l.[Price] * lc.[MemberCount] + ISNULL(l.[PrizeFundOverride], 0)) AS TotalPrizeFund,
            l.[Price] AS EntryFee,
            l.[IsFree],

            ISNULL(lc.[UserRoundsWon], 0) AS RoundsWon,
            ISNULL(lc.[UserMonthsWon], 0) AS MonthsWon,

            CAST(CASE
                WHEN (SELECT COUNT(*) FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] = @CompletedStatus) >= l.[NumberOfRounds]
                THEN 1
                ELSE 0
            END AS bit) AS IsFinished,
            l.[IsArchivedByUser],

            asr.[StageName],
            asr.[StageRank],
            asr.[PreRoundStageRank]

        FROM [MyLeagues] l
        LEFT JOIN [LeagueMemberStats] stats ON l.[LeagueId] = stats.[LeagueId] AND l.[UserId] = stats.[UserId]
        LEFT JOIN [ActiveRounds] ar ON l.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
        LEFT JOIN [LeagueContext] lc ON l.[LeagueId] = lc.[LeagueId]
        LEFT JOIN [ActiveRoundMonthlyRanks] armr ON l.[LeagueId] = armr.[LeagueId] AND l.[UserId] = armr.[UserId]
        LEFT JOIN [ActiveStageRanks] asr ON l.[LeagueId] = asr.[LeagueId] AND l.[UserId] = asr.[UserId]
        LEFT JOIN [ActiveOverallRanks] aor ON l.[LeagueId] = aor.[LeagueId] AND l.[UserId] = aor.[UserId]
        LEFT JOIN [ActiveRoundRanks] arr ON l.[LeagueId] = arr.[LeagueId] AND l.[UserId] = arr.[UserId]
        LEFT JOIN [ActiveExactScoresRanks] aes ON l.[LeagueId] = aes.[LeagueId] AND l.[UserId] = aes.[UserId]

        ORDER BY
            CASE WHEN ar.[Status] = @InProgressStatus THEN 0 ELSE 1 END ASC,
            l.[SeasonStartDateUtc] ASC,
            l.[Price] DESC,
            l.[LeagueName];

        SET TRANSACTION ISOLATION LEVEL READ COMMITTED;";

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