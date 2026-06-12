using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using System.Data;

namespace ThePredictions.Infrastructure.Repositories;

public class LeagueStatsRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueStatsRepository
{
    public async Task SnapshotRanksForRoundStartAsync(int roundId, CancellationToken cancellationToken)
    {
        await EnsureMemberStatsRowsExistAsync(roundId, cancellationToken);

        const string sql = @"
            UPDATE lms
            SET
                lms.[SnapshotOverallRank] = lms.[OverallRank],
                lms.[SnapshotMonthRank] = lms.[MonthRank],
                lms.[LiveRoundRank] = 1,
                lms.[StableRoundRank] = 1,
                lms.[LiveRoundPoints] = 0,
                lms.[StableRoundPoints] = 0
            FROM [LeagueMemberStats] lms
            JOIN [Leagues] l ON lms.[LeagueId] = l.[Id]
            JOIN [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            WHERE r.[Id] = @RoundId";

        await Connection.ExecuteAsync(new CommandDefinition(sql, new { RoundId = roundId }, transaction: Transaction, cancellationToken: cancellationToken));
    }

    public async Task UpdateLiveStatsAsync(int roundId, CancellationToken cancellationToken)
    {
        await EnsureMemberStatsRowsExistAsync(roundId, cancellationToken);

        const string sql = @"

            UPDATE lms
            SET lms.[LiveRoundPoints] = lrr.[BoostedPoints]
            FROM [LeagueMemberStats] lms
            JOIN [LeagueRoundResults] lrr ON lms.[LeagueId] = lrr.[LeagueId] AND lms.[UserId] = lrr.[UserId]
            WHERE lrr.[RoundId] = @RoundId;

            WITH NewRanks AS (
                SELECT
                    lms.[LeagueId],
                    lms.[UserId],
                    RANK() OVER (PARTITION BY lms.[LeagueId] ORDER BY lms.[LiveRoundPoints] DESC) as NewRoundRank,
                    RANK() OVER (PARTITION BY lms.[LeagueId] ORDER BY (SELECT SUM([BoostedPoints]) FROM [LeagueRoundResults] WHERE [LeagueId] = lms.[LeagueId] AND [UserId] = lms.[UserId]) DESC) as NewOverallRank,
                    RANK() OVER (PARTITION BY lms.[LeagueId] ORDER BY (
                                                                            SELECT SUM(lrr.[BoostedPoints])
                                                                            FROM [LeagueRoundResults] lrr
                                                                            JOIN [Rounds] r ON lrr.[RoundId] = r.[Id]
                                                                            WHERE lrr.[LeagueId] = lms.[LeagueId]
                                                                            AND lrr.[UserId] = lms.[UserId]
                                                                            AND MONTH(r.[StartDateUtc]) = MONTH(GETUTCDATE()) AND YEAR(r.[StartDateUtc]) = YEAR(GETUTCDATE())
                                                                      ) DESC) as NewMonthRank
                FROM [LeagueMemberStats] lms
                JOIN [Leagues] l ON lms.[LeagueId] = l.[Id]
                JOIN [Rounds] r ON l.[SeasonId] = r.[SeasonId]
                WHERE r.[Id] = @RoundId
            )

            UPDATE lms
            SET
                lms.[LiveRoundRank] = nr.[NewRoundRank],
                lms.[OverallRank] = nr.[NewOverallRank],
                lms.[MonthRank] = nr.[NewMonthRank]
            FROM [LeagueMemberStats] lms
            JOIN [NewRanks] nr ON lms.[LeagueId] = nr.[LeagueId] AND lms.[UserId] = nr.[UserId];";

        await Connection.ExecuteAsync(new CommandDefinition(sql, new { RoundId = roundId }, transaction: Transaction, cancellationToken: cancellationToken));
    }

    public async Task UpdateStableStatsAsync(int roundId, CancellationToken cancellationToken)
    {
        await EnsureMemberStatsRowsExistAsync(roundId, cancellationToken);

        const string sql = @"
            WITH StableCalc AS (
                SELECT
                    lm.[LeagueId],
                    lm.[UserId],
                    SUM(
                        CASE
                           WHEN up.[Outcome] = @ExactScore THEN l.[PointsForExactScore]
                           WHEN up.[Outcome] = @CorrectResult THEN l.[PointsForCorrectResult]
                           ELSE 0
                        END
                    ) as StablePoints

                FROM [LeagueMembers] lm

                JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
                JOIN [Rounds] r ON l.[SeasonId] = r.[SeasonId]
                JOIN [Matches] m ON r.[Id] = m.[RoundId]
                JOIN [UserPredictions] up ON m.[Id] = up.[MatchId] AND up.[UserId] = lm.[UserId]

                WHERE
                    r.[Id] = @RoundId
                    AND m.[Status] = @CompletedStatus

                GROUP BY
                    lm.[LeagueId],
                    lm.[UserId]
            ),

            RankedStable AS (
                SELECT
                    stats.[LeagueId],
                    stats.[UserId],
                    ISNULL(sc.[StablePoints], 0) as FinalPoints,
                    RANK() OVER (PARTITION BY stats.[LeagueId] ORDER BY ISNULL(sc.[StablePoints], 0) DESC) as FinalRank
                FROM [LeagueMemberStats] stats
                JOIN [Leagues] l ON stats.[LeagueId] = l.[Id]
                JOIN [Rounds] r ON l.[SeasonId] = r.[SeasonId]
                LEFT JOIN [StableCalc] sc ON stats.[LeagueId] = sc.[LeagueId] AND stats.[UserId] = sc.[UserId]
                WHERE r.[Id] = @RoundId
            )

            UPDATE stats
            SET
                stats.[StableRoundPoints] = rs.[FinalPoints],
                stats.[StableRoundRank] = rs.[FinalRank]
            FROM [LeagueMemberStats] stats
            INNER JOIN [RankedStable] rs ON stats.[LeagueId] = rs.[LeagueId] AND stats.[UserId] = rs.[UserId];";

        await Connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            RoundId = roundId,
            CompletedStatus = nameof(MatchStatus.Completed),
            PredictionOutcome.ExactScore,
            PredictionOutcome.CorrectResult
        }, transaction: Transaction, cancellationToken: cancellationToken));
    }

    // The snapshot/live/stable updates above only UPDATE existing rows, and nothing else in the
    // pipeline creates them, so a brand-new season's leagues would never get a [LeagueMemberStats]
    // row. That left the cached My Leagues tiles reading NULL (blank round rank, and an overall
    // rank that fell back to a fabricated "1st"). This ensures every approved member of every
    // league in the round's season has a row before the ranks are calculated. New rows start tied
    // first on zero points; the subsequent UPDATEs overwrite these with the real values.
    public async Task<int> EnsureMemberStatsRowsExistAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [LeagueMemberStats]
            (
                [LeagueId],
                [UserId],
                [OverallRank],
                [MonthRank],
                [LiveRoundRank],
                [SnapshotOverallRank],
                [SnapshotMonthRank],
                [StableRoundRank],
                [LiveRoundPoints],
                [StableRoundPoints]
            )
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                1,
                1,
                1,
                1,
                1,
                1,
                0,
                0
            FROM [LeagueMembers] lm
            JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            WHERE r.[Id] = @RoundId
                AND lm.[Status] = @ApprovedStatus
                AND NOT EXISTS (
                    SELECT 1
                    FROM [LeagueMemberStats] lms
                    WHERE lms.[LeagueId] = lm.[LeagueId]
                        AND lms.[UserId] = lm.[UserId]
                );";

        return await Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RoundId = roundId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            },
            transaction: Transaction,
            cancellationToken: cancellationToken));
    }
}
