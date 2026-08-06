using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetMyLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    public async Task<IEnumerable<MyLeagueDto>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
    {
        // Every per-member rank on this tile is read from the [LeagueMemberStats] cache, maintained by
        // LeagueStatsRepository on the write path. It used to be computed here instead, with one
        // RANK() OVER pass per tile over every member of every league the user is in. That was correct
        // but the query grew to the point where SQL Server spent ~400ms planning it, several times what
        // it spent running it, and the plan was being invalidated roughly once a minute by the
        // score-update job - so most dashboard loads during a live round paid the full compile. What is
        // left here is cheap league-level metadata: the active round, the stage name, member counts and
        // the prize pot.
        //
        // The cache columns and this SELECT are a contract. If you change what a rank means, change it
        // in LeagueStatsRepository.RecomputeAsync - not here - or the two will silently disagree.
        //
        // Runs under READ UNCOMMITTED. This is a high-frequency read that was blocking for several
        // seconds behind the results/stats write path, because the database does not have
        // READ_COMMITTED_SNAPSHOT enabled and it cannot be turned on for this managed instance. The tile
        // is a live view that auto-refreshes, so a transient dirty read self-corrects on the next poll,
        // whereas the multi-second lock wait was user-visible. The isolation level is reset at the end
        // of the batch so it cannot leak to other reads that reuse the pooled connection.
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

            stats.[OverallRank] AS Rank,
            -- The Month slot is relabelled 'Exact Scores' for a tournament and ranks by exact-score
            -- count instead of points. The cache stores both metrics under their own names; this is
            -- the one place the swap happens, so a tournament can never show a points rank here.
            CASE WHEN l.[CompetitionType] = 1 THEN stats.[ExactScoresRank] ELSE stats.[MonthRank] END AS MonthRank,
            CASE
                WHEN ar.[Status] = @PublishedStatus THEN 1
                ELSE stats.[LiveRoundRank]
            END AS RoundRank,

            stats.[SnapshotOverallRank] AS PreRoundOverallRank,
            CASE WHEN l.[CompetitionType] = 1 THEN stats.[PreRoundExactScoresRank] ELSE stats.[SnapshotMonthRank] END AS PreRoundMonthRank,
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

            rs.[StageName],
            stats.[StageRank],
            stats.[PreRoundStageRank]

        FROM [MyLeagues] l
        LEFT JOIN [LeagueMemberStats] stats ON l.[LeagueId] = stats.[LeagueId] AND l.[UserId] = stats.[UserId]
        LEFT JOIN [ActiveRounds] ar ON l.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
        LEFT JOIN [RoundStages] rs ON rs.[RoundId] = ar.[RoundId]
        LEFT JOIN [LeagueContext] lc ON l.[LeagueId] = lc.[LeagueId]

        ORDER BY
            CASE WHEN ar.[Status] = @InProgressStatus THEN 0 ELSE 1 END ASC,
            l.[SeasonStartDateUtc] ASC,
            l.[Price] DESC,
            l.[LeagueName];

        SET TRANSACTION ISOLATION LEVEL READ COMMITTED;";

        var leagues = await dbConnection.QueryAsync<MyLeagueQueryResult>(
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

        return leagues.Select(l => new MyLeagueDto(
            l.Id,
            l.Name,
            l.SeasonName,
            l.CompetitionType,
            l.SeasonStartDateUtc,
            l.EntryDeadlineUtc,
            l.CurrentRound,
            l.CurrentMonth,
            l.RoundStartDateUtc,
            l.MemberCount,
            l.Rank,
            l.MonthRank,
            l.RoundRank,
            l.PreRoundOverallRank,
            l.PreRoundMonthRank,
            l.StableRoundRank,
            l.RoundStatus,
            l.InProgressCount,
            l.CompletedCount,
            l.PrizeMoneyWon,
            l.PrizeMoneyRemaining,
            l.TotalPrizeFund,
            l.EntryFee,
            l.IsFree,
            l.RoundsWon,
            l.MonthsWon,
            l.IsFinished,
            l.IsArchivedByUser,
            l.StageName,
            l.StageRank,
            l.PreRoundStageRank));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record MyLeagueQueryResult(
        int Id,
        string Name,
        string SeasonName,
        CompetitionType CompetitionType,
        DateTime? SeasonStartDateUtc,
        DateTime? EntryDeadlineUtc,

        string CurrentRound,
        string CurrentMonth,
        DateTime? RoundStartDateUtc,
        int? MemberCount,

        int? Rank,
        int? MonthRank,
        int? RoundRank,

        int? PreRoundOverallRank,
        int? PreRoundMonthRank,
        int? StableRoundRank,
        string RoundStatus,
        int InProgressCount,
        int CompletedCount,

        decimal PrizeMoneyWon,
        decimal PrizeMoneyRemaining,
        decimal TotalPrizeFund,
        decimal EntryFee,
        bool IsFree,

        int RoundsWon,
        int MonthsWon,

        bool IsFinished,
        bool IsArchivedByUser,

        string? StageName,
        int? StageRank,
        int? PreRoundStageRank);
}
