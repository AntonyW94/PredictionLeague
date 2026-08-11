using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ISeasonRecapQuery"/>.
///
/// Five scoped reads in place of two statements holding four <c>RANK() OVER</c> windows, a running total over a
/// <c>CROSS JOIN</c> of rounds and members, and eleven <c>ISNULL</c> defaults. Nothing here ranks, averages,
/// totals or counts.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonRecapQuery(IApplicationReadDbConnection dbConnection) : ISeasonRecapQuery
{
    public async Task<SeasonRecapData?> ExecuteAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        var league = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(
            @"
            SELECT
                l.[Id],
                l.[SeasonId],
                l.[IsFree],
                l.[Price]
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;",
            cancellationToken,
            new { LeagueId = leagueId });

        if (league == null)
            return null;

        var membersTask = GetApprovedMembersAsync(leagueId, cancellationToken);
        var roundsTask = GetSeasonRoundsAsync(league.SeasonId, cancellationToken);
        var scoresTask = GetRoundScoresAsync(leagueId, cancellationToken);
        var exactScoresTask = GetExactScoreCountsAsync(league.SeasonId, userId, cancellationToken);
        var winningsTask = GetWinningAmountsAsync(leagueId, userId, cancellationToken);

        await Task.WhenAll(membersTask, roundsTask, scoresTask, exactScoresTask, winningsTask);

        return new SeasonRecapData(
            league.IsFree,
            league.Price,
            membersTask.Result,
            roundsTask.Result,
            scoresTask.Result,
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

    private async Task<IReadOnlyList<SeasonRecapRoundRow>> GetSeasonRoundsAsync(
        int seasonId, CancellationToken cancellationToken)
    {
        // Every round of the season, whatever its status: the trajectory steps through the completed ones even
        // where nobody scored, and the player's best and worst rounds ignore status altogether.
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                r.[RoundNumber],
                r.[StartDateUtc],
                r.[Status]
            FROM
                [Rounds] r
            WHERE
                r.[SeasonId] = @SeasonId;";

        return (await dbConnection.QueryAsync<SeasonRecapRoundRow>(
            sql, cancellationToken, new { SeasonId = seasonId })).ToList();
    }

    private async Task<IReadOnlyList<MemberRoundPointsByRoundRow>> GetRoundScoresAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        // Every member's scores, not just the player's: their final position, the rounds they won and the positions
        // they held can only be worked out against the rest of the league.
        const string sql = @"
            SELECT
                lrr.[UserId],
                lrr.[RoundId],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            WHERE
                lrr.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<MemberRoundPointsByRoundRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<int>> GetExactScoreCountsAsync(
        int seasonId, string userId, CancellationToken cancellationToken)
    {
        // Scoped to the one player because the recap never compares this with anyone. Totalling them is the rule.
        const string sql = @"
            SELECT
                rr.[ExactScoreCount]
            FROM
                [RoundResults] rr
            INNER JOIN
                [Rounds] r ON r.[Id] = rr.[RoundId]
            WHERE
                r.[SeasonId] = @SeasonId
                AND rr.[UserId] = @UserId;";

        return (await dbConnection.QueryAsync<int>(
            sql, cancellationToken, new { SeasonId = seasonId, UserId = userId })).ToList();
    }

    private async Task<IReadOnlyList<decimal>> GetWinningAmountsAsync(
        int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                w.[Amount]
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
            WHERE
                lps.[LeagueId] = @LeagueId
                AND w.[UserId] = @UserId;";

        return (await dbConnection.QueryAsync<decimal>(
            sql, cancellationToken, new { LeagueId = leagueId, UserId = userId })).ToList();
    }

    // Column order matches the SELECT above, per the Dapper result-mapping rule in CLAUDE.md.
    private sealed record LeagueRow(int Id, int SeasonId, bool IsFree, decimal Price);
}
