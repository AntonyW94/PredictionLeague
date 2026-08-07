using ThePredictions.Application.Data;

namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// Loads a user's earned badges plus the live progress metrics needed to compose the read models.
/// Shared by the page and tile query handlers. Progress is always computed fresh - never stored.
/// </summary>
internal static class BadgeStateQueries
{
    public static async Task<BadgeUserState> LoadAsync(IApplicationReadDbConnection dbConnection, string userId, CancellationToken cancellationToken)
    {
        const string earnedSql = @"
            SELECT
                [BadgeKey] AS BadgeKey,
                COUNT(*) AS Count,
                MAX([AwardedUtc]) AS LastAwardedUtc,
                MAX([Detail]) AS Detail
            FROM [UserBadges]
            WHERE [UserId] = @UserId
            GROUP BY [BadgeKey];";

        var earned = (await dbConnection.QueryAsync<EarnedBadge>(earnedSql, cancellationToken, new { UserId = userId }))
            .ToDictionary(e => e.BadgeKey);

        const string scalarsSql = @"
            SELECT
                ISNULL((
                    SELECT TOP 1 SUM(rr.[ExactScoreCount])
                    FROM [RoundResults] rr
                    JOIN [Rounds] r ON r.[Id] = rr.[RoundId]
                    WHERE rr.[UserId] = @UserId
                    GROUP BY r.[SeasonId]
                    ORDER BY r.[SeasonId] DESC), 0) AS SeasonExactTotal,
                ISNULL((SELECT MAX([ExactScoreCount]) FROM [RoundResults] WHERE [UserId] = @UserId), 0) AS BestExactsInRound,
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [UserId] = @UserId AND [Status] = 'Approved') AS LeaguesJoined;";

        var scalars = await dbConnection.QuerySingleOrDefaultAsync<MetricScalars>(scalarsSql, cancellationToken, new { UserId = userId })
                      ?? new MetricScalars(0, 0, 0);

        const string streakSql = @"
            WITH ScoredRounds AS (
                SELECT DISTINCT r.[SeasonId], r.[RoundNumber], r.[Id] AS RoundId
                FROM [Rounds] r
                WHERE EXISTS (SELECT 1 FROM [RoundResults] rr WHERE rr.[RoundId] = r.[Id])
            ),
            Grid AS (
                SELECT
                    sr.[SeasonId],
                    sr.[RoundNumber],
                    CASE WHEN rr.[ExactScoreCount] >= 1 THEN 1 ELSE 0 END AS Hit
                FROM ScoredRounds sr
                LEFT JOIN [RoundResults] rr ON rr.[RoundId] = sr.[RoundId] AND rr.[UserId] = @UserId
            ),
            Numbered AS (
                SELECT
                    g.[SeasonId],
                    g.[Hit],
                    ROW_NUMBER() OVER (PARTITION BY g.[SeasonId] ORDER BY g.[RoundNumber])
                      - ROW_NUMBER() OVER (PARTITION BY g.[SeasonId], g.[Hit] ORDER BY g.[RoundNumber]) AS Grp
                FROM Grid g
            ),
            Streaks AS (
                SELECT COUNT(*) AS Len
                FROM Numbered n
                WHERE n.[Hit] = 1
                GROUP BY n.[SeasonId], n.[Grp]
            )
            SELECT ISNULL(MAX(Len), 0) FROM Streaks;";

        var bestStreak = await dbConnection.QuerySingleOrDefaultAsync<int>(streakSql, cancellationToken, new { UserId = userId });

        // Current run: consecutive scored rounds with an exact in the user's most recent season, ending at
        // the latest scored round. Zero if they didn't score an exact in that latest round.
        const string currentStreakSql = @"
            WITH ScoredRounds AS (
                SELECT r.[Id] AS RoundId, r.[RoundNumber], r.[SeasonId]
                FROM [Rounds] r
                WHERE EXISTS (SELECT 1 FROM [RoundResults] rr WHERE rr.[RoundId] = r.[Id])
            ),
            UserSeason AS (
                SELECT TOP 1 sr.[SeasonId]
                FROM ScoredRounds sr
                JOIN [RoundResults] rr ON rr.[RoundId] = sr.[RoundId]
                WHERE rr.[UserId] = @UserId
                ORDER BY sr.[SeasonId] DESC
            ),
            SeasonRounds AS (
                SELECT sr.[RoundNumber], sr.[RoundId]
                FROM ScoredRounds sr
                WHERE sr.[SeasonId] = (SELECT [SeasonId] FROM UserSeason)
            ),
            MaxRn AS (SELECT MAX([RoundNumber]) AS M FROM SeasonRounds),
            Grid AS (
                SELECT
                    sr.[RoundNumber],
                    CASE WHEN rr.[ExactScoreCount] >= 1 THEN 1 ELSE 0 END AS Hit
                FROM SeasonRounds sr
                LEFT JOIN [RoundResults] rr ON rr.[RoundId] = sr.[RoundId] AND rr.[UserId] = @UserId
            ),
            Numbered AS (
                SELECT
                    g.[RoundNumber],
                    g.[Hit],
                    ROW_NUMBER() OVER (ORDER BY g.[RoundNumber])
                      - ROW_NUMBER() OVER (PARTITION BY g.[Hit] ORDER BY g.[RoundNumber]) AS Grp
                FROM Grid g
            ),
            Islands AS (
                SELECT COUNT(*) AS Len, MAX(n.[RoundNumber]) AS LastRn
                FROM Numbered n
                WHERE n.[Hit] = 1
                GROUP BY n.[Grp]
            )
            SELECT ISNULL((SELECT i.Len FROM Islands i JOIN MaxRn m ON i.LastRn = m.M), 0);";

        var currentStreak = await dbConnection.QuerySingleOrDefaultAsync<int>(currentStreakSql, cancellationToken, new { UserId = userId });

        const string everPresentSql = @"
            WITH Season AS (
                SELECT TOP 1 r.[SeasonId]
                FROM [UserPredictions] up
                JOIN [Matches] m ON m.[Id] = up.[MatchId]
                JOIN [Rounds] r ON r.[Id] = m.[RoundId]
                WHERE up.[UserId] = @UserId
                ORDER BY r.[SeasonId] DESC
            ),
            CompletedRounds AS (
                SELECT
                    r.[Id] AS RoundId,
                    (SELECT COUNT(*) FROM [Matches] m WHERE m.[RoundId] = r.[Id]) AS MatchCount
                FROM [Rounds] r
                WHERE r.[SeasonId] = (SELECT [SeasonId] FROM Season)
                    AND r.[Status] = 'Completed'
            ),
            PredictedPerRound AS (
                SELECT
                    cr.[RoundId],
                    cr.[MatchCount],
                    (SELECT COUNT(*)
                     FROM [UserPredictions] up
                     JOIN [Matches] m ON m.[Id] = up.[MatchId]
                     WHERE m.[RoundId] = cr.[RoundId] AND up.[UserId] = @UserId) AS PredictedCount
                FROM CompletedRounds cr
            )
            SELECT
                (SELECT COUNT(*) FROM CompletedRounds) AS RoundsTotal,
                (SELECT COUNT(*) FROM PredictedPerRound WHERE MatchCount > 0 AND PredictedCount >= MatchCount) AS RoundsPredicted;";

        var everPresentRow = await dbConnection.QuerySingleOrDefaultAsync<EverPresentRow>(everPresentSql, cancellationToken, new { UserId = userId });

        var everPresent = everPresentRow is { RoundsTotal: > 0 }
            ? new EverPresentProgress(everPresentRow.RoundsPredicted, everPresentRow.RoundsTotal, everPresentRow.RoundsPredicted < everPresentRow.RoundsTotal)
            : null;

        var metrics = new BadgeProgressMetrics(scalars.SeasonExactTotal, scalars.BestExactsInRound, bestStreak, currentStreak, scalars.LeaguesJoined, everPresent);

        return new BadgeUserState(earned, metrics);
    }

    // internal so a test can supply rows to the shaping above; InternalsVisibleTo already exposes
    // this assembly to ThePredictions.Application.Tests.Unit.
    internal sealed record MetricScalars(int SeasonExactTotal, int BestExactsInRound, int LeaguesJoined);

    internal sealed record EverPresentRow(int RoundsTotal, int RoundsPredicted);
}
