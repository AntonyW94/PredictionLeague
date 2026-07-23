using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Badges.Evaluation;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Infrastructure.Repositories;

public class BadgeEvaluationRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IBadgeEvaluationRepository
{
    public async Task<IReadOnlyList<RoundUserResult>> GetRoundResultsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [UserId] AS UserId,
                [ExactScoreCount] AS ExactScoreCount,
                [TotalPoints] AS TotalPoints
            FROM [RoundResults]
            WHERE [RoundId] = @RoundId;";

        return (await QueryAsync<RoundUserResult>(sql, new { RoundId = roundId }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<UserCount>> GetSeasonCumulativeExactsAsync(int seasonId, int roundNumber, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                rr.[UserId] AS UserId,
                SUM(rr.[ExactScoreCount]) AS Count
            FROM [RoundResults] rr
            JOIN [Rounds] r ON r.[Id] = rr.[RoundId]
            WHERE r.[SeasonId] = @SeasonId
                AND r.[RoundNumber] <= @RoundNumber
            GROUP BY rr.[UserId];";

        return (await QueryAsync<UserCount>(sql, new { SeasonId = seasonId, RoundNumber = roundNumber }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<UserCount>> GetStreaksEndingAtRoundAsync(int seasonId, int roundNumber, CancellationToken cancellationToken)
    {
        // Length of the current run of consecutive scored rounds (<= this round) ending at the most recent
        // scored round, per user, where each round in the run had at least one exact score. Only users whose
        // run reaches the latest scored round qualify (i.e. they scored an exact in this round).
        const string sql = @"
            WITH ScoredRounds AS (
                SELECT r.[Id] AS RoundId, r.[RoundNumber]
                FROM [Rounds] r
                WHERE r.[SeasonId] = @SeasonId
                    AND r.[RoundNumber] <= @RoundNumber
                    AND EXISTS (SELECT 1 FROM [RoundResults] rr WHERE rr.[RoundId] = r.[Id])
            ),
            MaxRound AS (SELECT MAX([RoundNumber]) AS MaxRn FROM ScoredRounds),
            Users AS (
                SELECT DISTINCT rr.[UserId]
                FROM [RoundResults] rr
                JOIN ScoredRounds sr ON sr.[RoundId] = rr.[RoundId]
            ),
            Grid AS (
                SELECT
                    u.[UserId],
                    sr.[RoundNumber],
                    CASE WHEN rr.[ExactScoreCount] >= 1 THEN 1 ELSE 0 END AS Hit
                FROM Users u
                CROSS JOIN ScoredRounds sr
                LEFT JOIN [RoundResults] rr ON rr.[RoundId] = sr.[RoundId] AND rr.[UserId] = u.[UserId]
            ),
            Numbered AS (
                SELECT
                    g.[UserId],
                    g.[RoundNumber],
                    g.[Hit],
                    ROW_NUMBER() OVER (PARTITION BY g.[UserId] ORDER BY g.[RoundNumber])
                      - ROW_NUMBER() OVER (PARTITION BY g.[UserId], g.[Hit] ORDER BY g.[RoundNumber]) AS Grp
                FROM Grid g
            ),
            Islands AS (
                SELECT n.[UserId], COUNT(*) AS Len, MAX(n.[RoundNumber]) AS LastRn
                FROM Numbered n
                WHERE n.[Hit] = 1
                GROUP BY n.[UserId], n.[Grp]
            )
            SELECT i.[UserId] AS UserId, i.[Len] AS Count
            FROM Islands i
            JOIN MaxRound m ON i.[LastRn] = m.MaxRn;";

        return (await QueryAsync<UserCount>(sql, new { SeasonId = seasonId, RoundNumber = roundNumber }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<UserLeague>> GetRoundWinnersAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH Ranked AS (
                SELECT
                    [UserId],
                    [LeagueId],
                    RANK() OVER (PARTITION BY [LeagueId] ORDER BY [BoostedPoints] DESC) AS Rnk
                FROM [LeagueRoundResults]
                WHERE [RoundId] = @RoundId
            )
            SELECT [UserId] AS UserId, [LeagueId] AS LeagueId
            FROM Ranked
            WHERE Rnk = 1;";

        return (await QueryAsync<UserLeague>(sql, new { RoundId = roundId }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<string>> GetBeatTheCrowdUsersAsync(int roundId, int minimumCrowd, CancellationToken cancellationToken)
    {
        // A user "beats the crowd" when they backed the actual result AND that result was the strictly
        // least-backed of the (2+) outcomes the crowd predicted, in a crowd of at least @MinimumCrowd.
        const string sql = @"
            WITH Preds AS (
                SELECT
                    up.[UserId],
                    up.[MatchId],
                    CASE WHEN up.[PredictedHomeScore] > up.[PredictedAwayScore] THEN 'H'
                         WHEN up.[PredictedHomeScore] < up.[PredictedAwayScore] THEN 'A'
                         ELSE 'D' END AS PredOutcome
                FROM [UserPredictions] up
                JOIN [Matches] m ON m.[Id] = up.[MatchId]
                WHERE m.[RoundId] = @RoundId
                    AND m.[Status] = 'Completed'
                    AND m.[ActualHomeTeamScore] IS NOT NULL
                    AND m.[ActualAwayTeamScore] IS NOT NULL
            ),
            Actual AS (
                SELECT
                    m.[Id] AS MatchId,
                    CASE WHEN m.[ActualHomeTeamScore] > m.[ActualAwayTeamScore] THEN 'H'
                         WHEN m.[ActualHomeTeamScore] < m.[ActualAwayTeamScore] THEN 'A'
                         ELSE 'D' END AS ActualOutcome
                FROM [Matches] m
                WHERE m.[RoundId] = @RoundId
                    AND m.[Status] = 'Completed'
                    AND m.[ActualHomeTeamScore] IS NOT NULL
                    AND m.[ActualAwayTeamScore] IS NOT NULL
            ),
            Counts AS (
                SELECT p.[MatchId], p.[PredOutcome], COUNT(*) AS Cnt
                FROM Preds p
                GROUP BY p.[MatchId], p.[PredOutcome]
            ),
            MatchTotals AS (
                SELECT [MatchId], SUM(Cnt) AS Total, COUNT(*) AS DistinctOutcomes, MIN(Cnt) AS MinCnt
                FROM Counts
                GROUP BY [MatchId]
            ),
            MinorityMatches AS (
                SELECT a.[MatchId], a.[ActualOutcome]
                FROM Actual a
                JOIN MatchTotals mt ON mt.[MatchId] = a.[MatchId]
                JOIN Counts ac ON ac.[MatchId] = a.[MatchId] AND ac.[PredOutcome] = a.[ActualOutcome]
                WHERE mt.[Total] >= @MinimumCrowd
                    AND mt.[DistinctOutcomes] >= 2
                    AND ac.[Cnt] = mt.[MinCnt]
                    AND (SELECT COUNT(*) FROM Counts c WHERE c.[MatchId] = a.[MatchId] AND c.[Cnt] = mt.[MinCnt]) = 1
            )
            SELECT DISTINCT p.[UserId]
            FROM Preds p
            JOIN MinorityMatches mm ON mm.[MatchId] = p.[MatchId] AND p.[PredOutcome] = mm.[ActualOutcome];";

        return (await QueryAsync<string>(sql, new { RoundId = roundId, MinimumCrowd = minimumCrowd }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<SocialiteAward>> GetSocialiteAwardsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH Ordered AS (
                SELECT
                    [UserId],
                    COALESCE([ApprovedAtUtc], [JoinedAtUtc]) AS JoinDate,
                    ROW_NUMBER() OVER (PARTITION BY [UserId] ORDER BY COALESCE([ApprovedAtUtc], [JoinedAtUtc]), [LeagueId]) AS Rn
                FROM [LeagueMembers]
                WHERE [Status] = 'Approved'
            )
            SELECT [UserId] AS UserId, [Rn] AS Rank, [JoinDate] AS AwardedUtc
            FROM Ordered
            WHERE [Rn] IN (1, 3, 5);";

        return (await QueryAsync<SocialiteAward>(sql, null, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<UserLeagueRank>> GetSeasonStandingsAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH Totals AS (
                SELECT
                    lrr.[LeagueId],
                    lrr.[UserId],
                    SUM(lrr.[BoostedPoints]) AS Pts
                FROM [LeagueRoundResults] lrr
                JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                WHERE r.[SeasonId] = @SeasonId
                GROUP BY lrr.[LeagueId], lrr.[UserId]
            ),
            Ranked AS (
                SELECT
                    [LeagueId],
                    [UserId],
                    RANK() OVER (PARTITION BY [LeagueId] ORDER BY Pts DESC) AS Rnk
                FROM Totals
            )
            SELECT [UserId] AS UserId, [LeagueId] AS LeagueId, [Rnk] AS Rank
            FROM Ranked;";

        return (await QueryAsync<UserLeagueRank>(sql, new { SeasonId = seasonId }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<string>> GetEverPresentUsersAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH CompletedRounds AS (
                SELECT r.[Id] AS RoundId
                FROM [Rounds] r
                WHERE r.[SeasonId] = @SeasonId AND r.[Status] = 'Completed'
            ),
            SeasonMatches AS (
                SELECT m.[Id] AS MatchId
                FROM [Matches] m
                JOIN CompletedRounds cr ON cr.[RoundId] = m.[RoundId]
            ),
            TotalMatches AS (SELECT COUNT(*) AS Cnt FROM SeasonMatches),
            UserPreds AS (
                SELECT up.[UserId], COUNT(*) AS Cnt
                FROM [UserPredictions] up
                JOIN SeasonMatches sm ON sm.[MatchId] = up.[MatchId]
                GROUP BY up.[UserId]
            )
            SELECT up.[UserId]
            FROM UserPreds up
            CROSS JOIN TotalMatches t
            WHERE t.Cnt > 0 AND up.Cnt >= t.Cnt;";

        return (await QueryAsync<string>(sql, new { SeasonId = seasonId }, cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<int>> GetCompletedRoundIdsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT r.[Id]
            FROM [Rounds] r
            WHERE r.[Status] = 'Completed'
            ORDER BY r.[SeasonId], r.[RoundNumber];";

        return (await QueryAsync<int>(sql, null, cancellationToken)).ToList();
    }

    private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(sql, parameters, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.QueryAsync<T>(command);
    }
}
