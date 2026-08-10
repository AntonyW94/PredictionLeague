using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class LeagueStatsRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueStatsRepository
{
    public Task RefreshLeagueAsync(int leagueId, CancellationToken cancellationToken)
    {
        return RecomputeAsync(leagueId, seasonId: null, cancellationToken);
    }

    public Task RefreshSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        return RecomputeAsync(leagueId: null, seasonId, cancellationToken);
    }

    // Rebuilds every cached rank for the leagues in scope, from scratch, in one batch.
    //
    // The cache used to be maintained as two half-updates - a "snapshot" taken at the moment a round
    // went live, plus a "live" update after each results change - which meant a cached value could only
    // ever be as correct as the ordering of the calls that produced it. Every historical bug in this
    // table came from that: a snapshot taken after the scores had already landed, a rank never
    // recomputed because the triggering event was not wired up, a row that was never created at all.
    //
    // Every one of these values is instead a pure function of the current results and the league's
    // active round, including the pre-round ones (a "pre-round" rank is just a rank over the rounds
    // before the active one - it does not need to have been captured at that moment). So this recompute
    // derives all of them together and is idempotent: run it as often as you like, in any order, and it
    // converges on the same answer. The trade-off is that it always does the full league rather than a
    // delta, which is why it is a handful of set-based statements over small tables rather than a
    // per-member update.
    //
    // The active-round resolution and every ORDER BY below MUST stay in lockstep with
    // GetMyLeaguesQueryHandler, which reads these columns. Those definitions are the contract between
    // the two files; if you change a metric here, change it there.
    private async Task RecomputeAsync(int? leagueId, int? seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            IF OBJECT_ID('tempdb..#LeagueActive') IS NOT NULL DROP TABLE #LeagueActive;
            IF OBJECT_ID('tempdb..#MemberMeasures') IS NOT NULL DROP TABLE #MemberMeasures;
            IF OBJECT_ID('tempdb..#StablePoints') IS NOT NULL DROP TABLE #StablePoints;

            -- 1. The leagues in scope, each resolved to the round the My Leagues tile is currently
            --    showing, plus the three 'is there anything earlier to compare against?' flags that
            --    decide whether a pre-round rank exists at all. A league whose season has no
            --    non-draft round has no active round, and therefore no ranks.
            SELECT
                l.[Id] AS LeagueId,
                l.[SeasonId],
                ar.[RoundId] AS ActiveRoundId,
                ar.[RoundNumber] AS ActiveRoundNumber,
                ar.[StartDateUtc] AS ActiveRoundStartDateUtc,
                CASE
                    WHEN trm.[SeasonId] IS NULL THEN NULL
                    WHEN trm.[Stages] LIKE '%Group%' THEN 'Group Stage'
                    ELSE 'Knockout Stage'
                END AS ActiveStageName,
                CASE
                    WHEN ar.[RoundId] IS NULL THEN 0
                    WHEN EXISTS (
                        SELECT 1
                        FROM [Rounds] r2
                        WHERE r2.[SeasonId] = l.[SeasonId]
                            AND r2.[RoundNumber] < ar.[RoundNumber]
                            AND r2.[Status] IN (@InProgressRoundStatus, @CompletedRoundStatus)
                    ) THEN 1
                    ELSE 0
                END AS HasEarlierRound,
                CASE
                    WHEN ar.[RoundId] IS NULL THEN 0
                    WHEN EXISTS (
                        SELECT 1
                        FROM [Rounds] r2
                        WHERE r2.[SeasonId] = l.[SeasonId]
                            AND MONTH(r2.[StartDateUtc]) = MONTH(ar.[StartDateUtc])
                            AND YEAR(r2.[StartDateUtc]) = YEAR(ar.[StartDateUtc])
                            AND r2.[Id] <> ar.[RoundId]
                            AND r2.[Status] IN (@InProgressRoundStatus, @CompletedRoundStatus)
                    ) THEN 1
                    ELSE 0
                END AS HasOtherRoundThisMonth,
                CASE
                    WHEN ar.[RoundId] IS NULL THEN 0
                    WHEN EXISTS (
                        SELECT 1
                        FROM [Rounds] r2
                        JOIN [TournamentRoundMappings] trm2 ON trm2.[SeasonId] = r2.[SeasonId] AND trm2.[RoundNumber] = r2.[RoundNumber]
                        WHERE r2.[SeasonId] = l.[SeasonId]
                            AND r2.[Id] <> ar.[RoundId]
                            AND r2.[Status] IN (@InProgressRoundStatus, @CompletedRoundStatus)
                            AND CASE WHEN trm2.[Stages] LIKE '%Group%' THEN 'Group Stage' ELSE 'Knockout Stage' END
                                = CASE WHEN trm.[Stages] LIKE '%Group%' THEN 'Group Stage' ELSE 'Knockout Stage' END
                    ) THEN 1
                    ELSE 0
                END AS HasOtherRoundThisStage
            INTO #LeagueActive
            FROM [Leagues] l
            LEFT JOIN (
                SELECT
                    x.[SeasonId],
                    x.[RoundId],
                    x.[RoundNumber],
                    x.[StartDateUtc]
                FROM (
                    SELECT
                        r.[SeasonId],
                        r.[Id] AS RoundId,
                        r.[RoundNumber],
                        r.[StartDateUtc],
                        ROW_NUMBER() OVER (
                            PARTITION BY r.[SeasonId]
                            ORDER BY
                                CASE
                                    WHEN r.[Status] = @InProgressRoundStatus THEN 0
                                    WHEN r.[Status] = @CompletedRoundStatus AND r.[CompletedDateUtc] > DATEADD(HOUR, -48, GETUTCDATE()) THEN 1
                                    WHEN r.[Status] = @PublishedRoundStatus THEN 2
                                    ELSE 3
                                END ASC,
                                r.[RoundNumber] ASC
                        ) AS PriorityRank
                    FROM [Rounds] r
                    WHERE r.[Status] <> @DraftRoundStatus
                        AND EXISTS (
                            SELECT 1
                            FROM [Leagues] sl
                            WHERE sl.[SeasonId] = r.[SeasonId]
                                AND (@LeagueId IS NULL OR sl.[Id] = @LeagueId)
                                AND (@SeasonId IS NULL OR sl.[SeasonId] = @SeasonId)
                        )
                ) x
                WHERE x.[PriorityRank] = 1
            ) ar ON ar.[SeasonId] = l.[SeasonId]
            LEFT JOIN [TournamentRoundMappings] trm ON trm.[SeasonId] = l.[SeasonId] AND trm.[RoundNumber] = ar.[RoundNumber]
            WHERE (@LeagueId IS NULL OR l.[Id] = @LeagueId)
                AND (@SeasonId IS NULL OR l.[SeasonId] = @SeasonId);

            ALTER TABLE #LeagueActive ADD PRIMARY KEY ([LeagueId]);

            -- 2. Drop rows for anyone who is no longer an approved member. Nothing else deletes them,
            --    so a member who left or was rejected would otherwise keep occupying a rank position
            --    and push everyone below them down one.
            DELETE lms
            FROM [LeagueMemberStats] lms
            JOIN #LeagueActive la ON la.[LeagueId] = lms.[LeagueId]
            WHERE NOT EXISTS (
                SELECT 1
                FROM [LeagueMembers] lm
                WHERE lm.[LeagueId] = lms.[LeagueId]
                    AND lm.[UserId] = lms.[UserId]
                    AND lm.[Status] = @ApprovedMemberStatus
            );

            -- 3. Create rows for anyone who does not have one yet. The ranks are left NULL because a
            --    rank we have not computed is not a rank - writing a placeholder here is what used to
            --    surface as a fabricated '1st' on a blank tile.
            INSERT INTO [LeagueMemberStats]
            (
                [LeagueId],
                [UserId],
                [LiveRoundPoints],
                [StableRoundPoints]
            )
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                0,
                0
            FROM [LeagueMembers] lm
            JOIN #LeagueActive la ON la.[LeagueId] = lm.[LeagueId]
            WHERE lm.[Status] = @ApprovedMemberStatus
                AND NOT EXISTS (
                    SELECT 1
                    FROM [LeagueMemberStats] lms
                    WHERE lms.[LeagueId] = lm.[LeagueId]
                        AND lms.[UserId] = lm.[UserId]
                );

            -- 4. Every measure the ranks are built from, for every approved member, in one pass over
            --    the season's rounds. Driving from [Rounds] and left-joining the results keeps a member
            --    with no result row at zero rather than absent, which is what the tile expects.
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                ISNULL(SUM(lrr.[BoostedPoints]), 0) AS OverallPoints,
                ISNULL(SUM(CASE WHEN rnd.[RoundNumber] < la.[ActiveRoundNumber] THEN lrr.[BoostedPoints] ELSE 0 END), 0) AS PreRoundOverallPoints,
                ISNULL(SUM(CASE
                    WHEN MONTH(rnd.[StartDateUtc]) = MONTH(la.[ActiveRoundStartDateUtc])
                        AND YEAR(rnd.[StartDateUtc]) = YEAR(la.[ActiveRoundStartDateUtc])
                    THEN lrr.[BoostedPoints] ELSE 0 END), 0) AS MonthPoints,
                ISNULL(SUM(CASE
                    WHEN MONTH(rnd.[StartDateUtc]) = MONTH(la.[ActiveRoundStartDateUtc])
                        AND YEAR(rnd.[StartDateUtc]) = YEAR(la.[ActiveRoundStartDateUtc])
                        AND rnd.[Id] <> la.[ActiveRoundId]
                    THEN lrr.[BoostedPoints] ELSE 0 END), 0) AS PreRoundMonthPoints,
                ISNULL(SUM(CASE
                    WHEN CASE WHEN trm.[SeasonId] IS NULL THEN NULL WHEN trm.[Stages] LIKE '%Group%' THEN 'Group Stage' ELSE 'Knockout Stage' END = la.[ActiveStageName]
                    THEN lrr.[BoostedPoints] ELSE 0 END), 0) AS StagePoints,
                ISNULL(SUM(CASE
                    WHEN CASE WHEN trm.[SeasonId] IS NULL THEN NULL WHEN trm.[Stages] LIKE '%Group%' THEN 'Group Stage' ELSE 'Knockout Stage' END = la.[ActiveStageName]
                        AND rnd.[Id] <> la.[ActiveRoundId]
                    THEN lrr.[BoostedPoints] ELSE 0 END), 0) AS PreRoundStagePoints,
                ISNULL(SUM(CASE WHEN rnd.[Id] = la.[ActiveRoundId] THEN lrr.[BoostedPoints] ELSE 0 END), 0) AS LiveRoundPoints,
                ISNULL(SUM(rr.[ExactScoreCount]), 0) AS ExactScores,
                ISNULL(SUM(CASE WHEN rnd.[RoundNumber] < la.[ActiveRoundNumber] THEN rr.[ExactScoreCount] ELSE 0 END), 0) AS PreRoundExactScores
            INTO #MemberMeasures
            FROM [LeagueMembers] lm
            JOIN #LeagueActive la ON la.[LeagueId] = lm.[LeagueId]
            JOIN [Rounds] rnd ON rnd.[SeasonId] = la.[SeasonId]
            LEFT JOIN [LeagueRoundResults] lrr ON lrr.[LeagueId] = lm.[LeagueId] AND lrr.[UserId] = lm.[UserId] AND lrr.[RoundId] = rnd.[Id]
            LEFT JOIN [TournamentRoundMappings] trm ON trm.[SeasonId] = rnd.[SeasonId] AND trm.[RoundNumber] = rnd.[RoundNumber]
            LEFT JOIN [RoundResults] rr ON rr.[RoundId] = rnd.[Id] AND rr.[UserId] = lm.[UserId]
            WHERE lm.[Status] = @ApprovedMemberStatus
                AND la.[ActiveRoundId] IS NOT NULL
            GROUP BY lm.[LeagueId], lm.[UserId];

            -- A unique nonclustered index rather than a primary key. A clustered key is capped at 900
            -- bytes and [UserId] is nvarchar(450), so ([LeagueId], [UserId]) comes to 904 and SQL Server
            -- warns on every single execution that a long enough id would fail the insert. The
            -- nonclustered limit is 1,700 bytes, which this sits comfortably inside, and the uniqueness
            -- still asserts what matters: that the GROUP BY produced exactly one row per member.
            CREATE UNIQUE NONCLUSTERED INDEX [IX_MemberMeasures_League_User] ON #MemberMeasures ([LeagueId], [UserId]);

            -- 5. The 'stable' round points are a different metric with a different source: the
            --    league's own points-per-outcome settings applied to predictions on matches that have
            --    actually finished, so the number does not move while a match is in play.
            --
            --    The boost has to be applied here too, and getting that wrong is what made the round
            --    change arrow lie. [StableRoundRank] is the arrow's baseline and [LiveRoundRank] is its
            --    current value, so the pair must differ by *which matches count* and nothing else. A
            --    round-long boost is already in force before the live matches kick off, so leaving it
            --    off this side made the arrow credit the entire boost to the live window: a boosted
            --    member appeared to climb, and every unboosted member appeared to fall, during a match
            --    in which no points had been scored at all.
            --
            --    Rather than restate the boost arithmetic here - the rule lives in
            --    LeagueRoundResult.ApplyBoost and should not be duplicated - the multiplier is read back
            --    out of the data as [BoostedPoints] / [BasePoints] for the same member and round. That
            --    is exact for any multiplicative boost and adapts on its own if another is added. It is
            --    applied as SUM(...) * MAX(boosted) / MAX(base), multiplying before dividing so integer
            --    arithmetic stays exact. MAX() is just a way to reach a value that is constant per
            --    group. When there is no result row, or base points are zero, the raw total is used
            --    unchanged - it is necessarily zero in that case, since finished-match points are a
            --    subset of the round's points.
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                CASE
                    WHEN MAX(ISNULL(lrr.[BasePoints], 0)) = 0
                    THEN SUM(CASE
                        WHEN up.[Outcome] = @ExactScoreOutcome THEN l.[PointsForExactScore]
                        WHEN up.[Outcome] = @CorrectResultOutcome THEN l.[PointsForCorrectResult]
                        ELSE 0
                    END)
                    ELSE SUM(CASE
                        WHEN up.[Outcome] = @ExactScoreOutcome THEN l.[PointsForExactScore]
                        WHEN up.[Outcome] = @CorrectResultOutcome THEN l.[PointsForCorrectResult]
                        ELSE 0
                    END) * MAX(lrr.[BoostedPoints]) / MAX(lrr.[BasePoints])
                END AS StablePoints
            INTO #StablePoints
            FROM [LeagueMembers] lm
            JOIN #LeagueActive la ON la.[LeagueId] = lm.[LeagueId]
            JOIN [Leagues] l ON l.[Id] = lm.[LeagueId]
            JOIN [Matches] m ON m.[RoundId] = la.[ActiveRoundId] AND m.[Status] = @CompletedMatchStatus
            JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = lm.[UserId]
            -- One row per member per round, so this cannot fan the aggregate out.
            LEFT JOIN [LeagueRoundResults] lrr ON lrr.[LeagueId] = lm.[LeagueId]
                AND lrr.[UserId] = lm.[UserId]
                AND lrr.[RoundId] = la.[ActiveRoundId]
            WHERE lm.[Status] = @ApprovedMemberStatus
            GROUP BY lm.[LeagueId], lm.[UserId];

            CREATE UNIQUE NONCLUSTERED INDEX [IX_StablePoints_League_User] ON #StablePoints ([LeagueId], [UserId]);

            -- 6. Rank on every measure at once. A pre-round rank is NULL when there is nothing earlier
            --    to compare against (first round of the season, month or stage), and the stage ranks
            --    are NULL when the season has no stage mapping at all. NULL is what suppresses the
            --    change arrow on the tile, so these guards are load-bearing, not cosmetic.
            WITH Ranked AS (
                SELECT
                    mm.[LeagueId],
                    mm.[UserId],
                    mm.[LiveRoundPoints],
                    ISNULL(sp.[StablePoints], 0) AS StablePoints,
                    CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[OverallPoints] DESC) AS INT) AS OverallRank,
                    CASE WHEN la.[HasEarlierRound] = 0 THEN NULL
                         ELSE CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[PreRoundOverallPoints] DESC) AS INT) END AS PreRoundOverallRank,
                    CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[MonthPoints] DESC) AS INT) AS MonthRank,
                    CASE WHEN la.[HasOtherRoundThisMonth] = 0 THEN NULL
                         ELSE CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[PreRoundMonthPoints] DESC) AS INT) END AS PreRoundMonthRank,
                    CASE WHEN la.[ActiveStageName] IS NULL THEN NULL
                         ELSE CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[StagePoints] DESC) AS INT) END AS StageRank,
                    CASE WHEN la.[ActiveStageName] IS NULL OR la.[HasOtherRoundThisStage] = 0 THEN NULL
                         ELSE CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[PreRoundStagePoints] DESC) AS INT) END AS PreRoundStageRank,
                    CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[LiveRoundPoints] DESC) AS INT) AS LiveRoundRank,
                    CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY ISNULL(sp.[StablePoints], 0) DESC) AS INT) AS StableRoundRank,
                    CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[ExactScores] DESC) AS INT) AS ExactScoresRank,
                    CASE WHEN la.[HasEarlierRound] = 0 THEN NULL
                         ELSE CAST(RANK() OVER (PARTITION BY mm.[LeagueId] ORDER BY mm.[PreRoundExactScores] DESC) AS INT) END AS PreRoundExactScoresRank
                FROM #MemberMeasures mm
                JOIN #LeagueActive la ON la.[LeagueId] = mm.[LeagueId]
                LEFT JOIN #StablePoints sp ON sp.[LeagueId] = mm.[LeagueId] AND sp.[UserId] = mm.[UserId]
            )

            UPDATE lms
            SET
                lms.[OverallRank] = r.[OverallRank],
                lms.[SnapshotOverallRank] = r.[PreRoundOverallRank],
                lms.[MonthRank] = r.[MonthRank],
                lms.[SnapshotMonthRank] = r.[PreRoundMonthRank],
                lms.[StageRank] = r.[StageRank],
                lms.[PreRoundStageRank] = r.[PreRoundStageRank],
                lms.[ExactScoresRank] = r.[ExactScoresRank],
                lms.[PreRoundExactScoresRank] = r.[PreRoundExactScoresRank],
                lms.[LiveRoundRank] = r.[LiveRoundRank],
                lms.[StableRoundRank] = r.[StableRoundRank],
                lms.[LiveRoundPoints] = r.[LiveRoundPoints],
                lms.[StableRoundPoints] = r.[StablePoints]
            FROM [LeagueMemberStats] lms
            JOIN [Ranked] r ON r.[LeagueId] = lms.[LeagueId] AND r.[UserId] = lms.[UserId];

            -- 7. A league with no active round has nothing to rank. Clear the ranks rather than
            --    leaving whatever the last active round produced, so the tile shows no position
            --    instead of a stale one.
            UPDATE lms
            SET
                lms.[OverallRank] = NULL,
                lms.[SnapshotOverallRank] = NULL,
                lms.[MonthRank] = NULL,
                lms.[SnapshotMonthRank] = NULL,
                lms.[StageRank] = NULL,
                lms.[PreRoundStageRank] = NULL,
                lms.[ExactScoresRank] = NULL,
                lms.[PreRoundExactScoresRank] = NULL,
                lms.[LiveRoundRank] = NULL,
                lms.[StableRoundRank] = NULL,
                lms.[LiveRoundPoints] = 0,
                lms.[StableRoundPoints] = 0
            FROM [LeagueMemberStats] lms
            JOIN #LeagueActive la ON la.[LeagueId] = lms.[LeagueId]
            WHERE la.[ActiveRoundId] IS NULL;

            DROP TABLE #StablePoints;
            DROP TABLE #MemberMeasures;
            DROP TABLE #LeagueActive;";

        var parameters = new
        {
            LeagueId = leagueId,
            SeasonId = seasonId,
            ApprovedMemberStatus = nameof(LeagueMemberStatus.Approved),
            DraftRoundStatus = nameof(RoundStatus.Draft),
            PublishedRoundStatus = nameof(RoundStatus.Published),
            InProgressRoundStatus = nameof(RoundStatus.InProgress),
            CompletedRoundStatus = nameof(RoundStatus.Completed),
            CompletedMatchStatus = nameof(MatchStatus.Completed),
            ExactScoreOutcome = PredictionOutcome.ExactScore,
            CorrectResultOutcome = PredictionOutcome.CorrectResult
        };

        await Connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            transaction: Transaction,
            cancellationToken: cancellationToken));
    }
}
