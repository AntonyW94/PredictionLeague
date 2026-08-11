using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Dashboard;

/// <summary>
/// The SQL Server reads behind <see cref="IMyLeaguesQuery"/>.
///
/// Four reads in place of the largest statement on the site: a four-part CTE with a windowed priority ordering, two
/// <c>RANK() OVER</c> windows, nine correlated subqueries and eleven <c>ISNULL</c> defaults. Nothing here ranks,
/// counts wins, labels a round, works out a prize pot or decides which round is the active one.
/// </summary>
/// <remarks>
/// Every read keeps the <c>READ UNCOMMITTED</c> hint the original carried, and for the reason recorded against it:
/// this is a high-frequency tile that was blocking for seconds behind the results and stats write path, because this
/// managed instance cannot have <c>READ_COMMITTED_SNAPSHOT</c> enabled. The tile auto-refreshes, so a transient dirty
/// read self-corrects on the next poll, whereas the lock wait was plainly visible. The level is reset at the end of
/// each batch so it cannot leak to another read on the same pooled connection.
///
/// The <c>LeagueMemberStats</c> read is a keyed lookup and stays that way. ADR-0015 exists because computing those
/// ranks live cost roughly 400ms of query <i>planning</i> per dashboard load - the plan was invalidated about once a
/// minute by the score-update job, so most loads during a live round paid the full compile. Splitting one enormous
/// statement into four small keyed ones works with that, not against it.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class MyLeaguesQuery(IApplicationReadDbConnection dbConnection) : IMyLeaguesQuery
{
    private const string ReadUncommitted = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;";
    private const string ReadCommitted = "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;";

    public async Task<MyLeaguesData> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        var leaguesTask = GetLeaguesAsync(userId, cancellationToken);
        var roundsTask = GetSeasonRoundsAsync(userId, cancellationToken);
        var scoresTask = GetRoundScoresAsync(userId, cancellationToken);
        var statsTask = GetStatsAsync(userId, cancellationToken);

        await Task.WhenAll(leaguesTask, roundsTask, scoresTask, statsTask);

        return new MyLeaguesData(leaguesTask.Result, roundsTask.Result, scoresTask.Result, statsTask.Result);
    }

    private async Task<IReadOnlyList<MyLeagueRow>> GetLeaguesAsync(string userId, CancellationToken cancellationToken)
    {
        // The four aggregates are counts and sums. What they mean - the pot, what is left of it, whether the season
        // is over - is decided in C#.
        var sql = $@"
            {ReadUncommitted}

            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[Price],
                l.[PrizeFundOverride],
                l.[IsFree],
                lm.[IsArchivedByUser],
                s.[Id] AS [SeasonId],
                s.[Name] AS [SeasonName],
                c.[Type] AS [CompetitionType],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                l.[EntryDeadlineUtc],
                s.[NumberOfRounds],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] counted
                    WHERE
                        counted.[LeagueId] = l.[Id]
                        AND counted.[Status] = @ApprovedStatus
                ) AS [MemberCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Rounds] r
                    WHERE
                        r.[SeasonId] = l.[SeasonId]
                        AND r.[Status] = @CompletedStatus
                ) AS [CompletedRoundCount],
                (
                    SELECT
                        ISNULL(SUM(w.[Amount]), 0)
                    FROM
                        [Winnings] w
                    INNER JOIN
                        [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
                    WHERE
                        lps.[LeagueId] = l.[Id]
                ) AS [TotalPaidOut],
                (
                    SELECT
                        ISNULL(SUM(w.[Amount]), 0)
                    FROM
                        [Winnings] w
                    INNER JOIN
                        [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
                    WHERE
                        lps.[LeagueId] = l.[Id]
                        AND w.[UserId] = @UserId
                ) AS [UserWinnings]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus;

            {ReadCommitted}";

        return (await dbConnection.QueryAsync<MyLeagueRow>(
            sql, cancellationToken,
            new
            {
                UserId = userId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed)
            })).ToList();
    }

    private async Task<IReadOnlyList<MyLeagueRoundRow>> GetSeasonRoundsAsync(string userId, CancellationToken cancellationToken)
    {
        // Every round of every season the player has a league in, drafts included: which of them can be the active
        // round, and which one wins, is the handler's rule. The stage text comes back raw for the same reason.
        var sql = $@"
            {ReadUncommitted}

            SELECT
                r.[Id] AS [RoundId],
                r.[SeasonId],
                r.[RoundNumber],
                r.[DisplayName],
                r.[StartDateUtc],
                r.[CompletedDateUtc],
                r.[Status],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                        AND m.[Status] = @InProgressStatus
                ) AS [InProgressMatchCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                        AND m.[Status] = @CompletedMatchStatus
                ) AS [CompletedMatchCount],
                trm.[Stages]
            FROM
                [Rounds] r
            LEFT JOIN
                [TournamentRoundMappings] trm ON trm.[SeasonId] = r.[SeasonId] AND trm.[RoundNumber] = r.[RoundNumber]
            WHERE
                r.[SeasonId] IN (
                    SELECT
                        l.[SeasonId]
                    FROM
                        [LeagueMembers] lm
                    INNER JOIN
                        [Leagues] l ON l.[Id] = lm.[LeagueId]
                    WHERE
                        lm.[UserId] = @UserId
                        AND lm.[Status] = @ApprovedStatus
                );

            {ReadCommitted}";

        return (await dbConnection.QueryAsync<MyLeagueRoundRow>(
            sql, cancellationToken,
            new
            {
                UserId = userId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(MatchStatus.InProgress),
                CompletedMatchStatus = nameof(MatchStatus.Completed)
            })).ToList();
    }

    private async Task<IReadOnlyList<MyLeagueRoundScoreRow>> GetRoundScoresAsync(string userId, CancellationToken cancellationToken)
    {
        // Every approved member's scores in the player's leagues. A round cannot be known to be won without the
        // scores it was won against, and counting those wins is the rule that moved.
        var sql = $@"
            {ReadUncommitted}

            SELECT
                lrr.[LeagueId],
                lrr.[UserId],
                lrr.[RoundId],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            INNER JOIN
                [LeagueMembers] scorer ON scorer.[LeagueId] = lrr.[LeagueId]
                    AND scorer.[UserId] = lrr.[UserId]
                    AND scorer.[Status] = @ApprovedStatus
            INNER JOIN
                [LeagueMembers] mine ON mine.[LeagueId] = lrr.[LeagueId]
                    AND mine.[UserId] = @UserId
                    AND mine.[Status] = @ApprovedStatus;

            {ReadCommitted}";

        return (await dbConnection.QueryAsync<MyLeagueRoundScoreRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<MyLeagueStatsRow>> GetStatsAsync(string userId, CancellationToken cancellationToken)
    {
        // A keyed lookup on the cache maintained by LeagueStatsRepository, exactly as before. Nothing is recomputed
        // here and nothing should be - see ADR-0015.
        var sql = $@"
            {ReadUncommitted}

            SELECT
                stats.[LeagueId],
                stats.[OverallRank],
                stats.[MonthRank],
                stats.[LiveRoundRank],
                stats.[SnapshotOverallRank],
                stats.[SnapshotMonthRank],
                stats.[StableRoundRank],
                stats.[StageRank],
                stats.[PreRoundStageRank],
                stats.[ExactScoresRank],
                stats.[PreRoundExactScoresRank]
            FROM
                [LeagueMemberStats] stats
            INNER JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = stats.[LeagueId]
                    AND lm.[UserId] = stats.[UserId]
                    AND lm.[Status] = @ApprovedStatus
            WHERE
                stats.[UserId] = @UserId;

            {ReadCommitted}";

        return (await dbConnection.QueryAsync<MyLeagueStatsRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}
