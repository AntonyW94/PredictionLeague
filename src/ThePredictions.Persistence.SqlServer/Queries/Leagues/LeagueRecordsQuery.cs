using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueRecordsQuery"/>.
///
/// Four reads in place of one statement's ten <c>OUTER APPLY</c> blocks. Nothing here picks a record holder,
/// counts a win, sums a total, names a player or labels a prize.
/// </summary>
/// <remarks>
/// These reads used to carry a <c>READ UNCOMMITTED</c> hint of their own: the records tile spans the
/// highest-contention tables in the schema (<c>LeagueRoundResults</c>, <c>RoundResults</c>, <c>Winnings</c>) and was
/// blocking for seconds behind the results and stats write path, because this managed instance cannot have
/// <c>READ_COMMITTED_SNAPSHOT</c> enabled. The queries themselves are fast; the lock wait was the cost. The whole
/// query side now reads that way - see <see cref="ThePredictions.Application.Data.IReadIsolationPolicy"/> and
/// ADR-0019 - so the hint no longer belongs in the handful of files that noticed first.
///
/// It stays an adapter concern rather than a handler one either way: it is a statement about how one engine takes
/// locks, and it means nothing to an adapter that does not take them.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueRecordsQuery(IApplicationReadDbConnection dbConnection) : ILeagueRecordsQuery
{
    public async Task<LeagueRecordsData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var league = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(
            @"
            SELECT
                l.[Id],
                l.[SeasonId],
                l.[IsFree]
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;",
            cancellationToken,
            new { LeagueId = leagueId });

        if (league == null)
            return null;

        var membersTask = GetApprovedMembersAsync(leagueId, cancellationToken);
        var roundScoresTask = GetRoundScoresAsync(leagueId, cancellationToken);
        var exactScoresTask = GetExactScoresAsync(leagueId, league.SeasonId, cancellationToken);
        var winningsTask = GetWinningsAsync(leagueId, cancellationToken);

        await Task.WhenAll(membersTask, roundScoresTask, exactScoresTask, winningsTask);

        return new LeagueRecordsData(
            league.IsFree,
            membersTask.Result,
            roundScoresTask.Result,
            exactScoresTask.Result,
            winningsTask.Result);
    }

    private async Task<IReadOnlyList<LeaderboardParticipantRow>> GetApprovedMembersAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[UserId],
                u.[FirstName],
                u.[LastName]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<LeaderboardParticipantRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<LeagueRecordRoundScoreRow>> GetRoundScoresAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        // Not filtered by league membership, because it does not need to be: the table is already per-league and
        // the handler decides who may hold a record. HasAnyPrediction answers the EXISTS the lowest-round block
        // used; whether that fact excludes a row is the handler's rule.
        const string sql = @"
            SELECT
                lrr.[UserId],
                u.[FirstName],
                u.[LastName],
                lrr.[RoundId],
                r.[RoundNumber],
                r.[StartDateUtc] AS [RoundStartDateUtc],
                r.[Status] AS [RoundStatus],
                lrr.[BoostedPoints],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [UserPredictions] up
                    INNER JOIN
                        [Matches] m ON m.[Id] = up.[MatchId]
                    WHERE
                        m.[RoundId] = lrr.[RoundId]
                        AND up.[UserId] = lrr.[UserId]
                ) THEN 1 ELSE 0 END AS bit) AS [HasAnyPrediction]
            FROM
                [LeagueRoundResults] lrr
            INNER JOIN
                [Rounds] r ON r.[Id] = lrr.[RoundId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lrr.[UserId]
            WHERE
                lrr.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<LeagueRecordRoundScoreRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<LeagueRecordExactScoreRow>> GetExactScoresAsync(
        int leagueId, int seasonId, CancellationToken cancellationToken)
    {
        // RoundResults is league-agnostic, so this one narrows to approved members at the source: reading every
        // player's whole season to discard most of it would be wasteful. It is an optimisation, not the rule - the
        // handler filters the same way regardless, so narrowing here can only remove rows it would remove anyway.
        const string sql = @"
            SELECT
                rr.[UserId],
                u.[FirstName],
                u.[LastName],
                r.[RoundNumber],
                rr.[ExactScoreCount]
            FROM
                [RoundResults] rr
            INNER JOIN
                [Rounds] r ON r.[Id] = rr.[RoundId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = rr.[UserId]
            INNER JOIN
                [LeagueMembers] lm ON lm.[UserId] = rr.[UserId]
                    AND lm.[LeagueId] = @LeagueId
                    AND lm.[Status] = @ApprovedStatus
            WHERE
                r.[SeasonId] = @SeasonId;";

        return (await dbConnection.QueryAsync<LeagueRecordExactScoreRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, SeasonId = seasonId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<LeagueRecordWinningRow>> GetWinningsAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        // [PrizeType] is declared nvarchar(20) but holds the enum's numeric value as text, because the write path
        // passes the enum and Dapper sends its underlying int. Mapping it to PrizeType here is what stops that
        // accident spreading: the old statement compared the column against an int parameter and worked only
        // because SQL Server silently converted one to the other.
        const string sql = @"
            SELECT
                w.[UserId],
                u.[FirstName],
                u.[LastName],
                w.[Amount],
                w.[AwardedDateUtc],
                lps.[PrizeType],
                lps.[PrizeDescription],
                w.[RoundNumber],
                w.[Month]
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = w.[UserId]
            WHERE
                lps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<LeagueRecordWinningRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    // Column order matches the SELECT above, per the Dapper result-mapping rule in CLAUDE.md.
    private sealed record LeagueRow(int Id, int SeasonId, bool IsFree);
}
