using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Boosts;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueBoostUsageQuery"/>.
///
/// The concurrency here is the point of the port being one method: which reads can run together is a
/// persistence decision, and it used to sit in the handler as a <c>Task.WhenAll</c>. Another adapter is free
/// to batch these differently, or serve them from a single statement, without the handler changing.
///
/// Every predicate below is scoping - which rows belong to this league and season. The rules that used to be
/// mixed in with them now live in C#: the secrecy comparison against <c>GETUTCDATE()</c>, the points-gained
/// <c>CASE</c>, the display-name concatenation, and the ordering.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueBoostUsageQuery(IApplicationReadDbConnection dbConnection) : ILeagueBoostUsageQuery
{
    public async Task<LeagueBoostUsageData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        // The season is needed as a parameter to the usage read, so it goes first and alone.
        var seasonId = await dbConnection.QuerySingleOrDefaultAsync<int?>(
            "SELECT l.[SeasonId] FROM [Leagues] l WHERE l.[Id] = @LeagueId;",
            cancellationToken,
            new { LeagueId = leagueId });

        if (seasonId == null)
            return null;

        var rulesTask = GetBoostRulesAsync(leagueId, cancellationToken);
        var windowsTask = GetWindowsAsync(leagueId, cancellationToken);
        var membersTask = GetMembersAsync(leagueId, cancellationToken);
        var usagesTask = GetUsagesAsync(leagueId, seasonId.Value, cancellationToken);
        var roundRangeTask = GetRoundRangeAsync(leagueId, cancellationToken);
        var inProgressTask = GetRoundNumberByStatusAsync(leagueId, RoundStatus.InProgress, ascending: true, cancellationToken);
        var lastCompletedTask = GetRoundNumberByStatusAsync(leagueId, RoundStatus.Completed, ascending: false, cancellationToken);

        await Task.WhenAll(rulesTask, windowsTask, membersTask, usagesTask, roundRangeTask, inProgressTask, lastCompletedTask);

        return new LeagueBoostUsageData(
            SeasonId: seasonId.Value,
            BoostRules: rulesTask.Result,
            Windows: windowsTask.Result,
            Members: membersTask.Result,
            Usages: usagesTask.Result,
            RoundRange: roundRangeTask.Result,
            InProgressRoundNumber: inProgressTask.Result,
            LastCompletedRoundNumber: lastCompletedTask.Result);
    }

    private async Task<IReadOnlyList<BoostRuleRow>> GetBoostRulesAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lbr.[Id] AS [LeagueBoostRuleId],
                bd.[Code] AS [BoostCode],
                bd.[Name],
                bd.[ImageUrl],
                lbr.[TotalUsesPerSeason]
            FROM
                [BoostDefinitions] bd
            INNER JOIN
                [LeagueBoostRules] lbr ON lbr.[BoostDefinitionId] = bd.[Id]
            WHERE
                lbr.[LeagueId] = @LeagueId
                AND lbr.[IsEnabled] = 1;";

        return (await dbConnection.QueryAsync<BoostRuleRow>(sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<BoostWindowRow>> GetWindowsAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lbw.[LeagueBoostRuleId],
                lbw.[StartRoundNumber],
                lbw.[EndRoundNumber],
                lbw.[MaxUsesInWindow]
            FROM
                [LeagueBoostWindows] lbw
            INNER JOIN
                [LeagueBoostRules] lbr ON lbw.[LeagueBoostRuleId] = lbr.[Id]
            WHERE
                lbr.[LeagueId] = @LeagueId
                AND lbr.[IsEnabled] = 1;";

        return (await dbConnection.QueryAsync<BoostWindowRow>(sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<BoostMemberRow>> GetMembersAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<BoostMemberRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<BoostUsageRow>> GetUsagesAsync(int leagueId, int seasonId, CancellationToken cancellationToken)
    {
        // Uncensored on purpose. Which of these the viewer may see is BoostUsageVisibility's decision, taken
        // against an injected clock rather than the database's.
        const string sql = @"
            SELECT
                ubu.[UserId],
                bd.[Code] AS [BoostCode],
                r.[RoundNumber],
                r.[DeadlineUtc] AS [RoundDeadlineUtc],
                CAST(CASE WHEN lrr.[HasBoost] = 1 THEN 1 ELSE 0 END AS bit) AS [HasBoost],
                lrr.[BasePoints],
                lrr.[BoostedPoints]
            FROM
                [UserBoostUsages] ubu
            INNER JOIN
                [BoostDefinitions] bd ON ubu.[BoostDefinitionId] = bd.[Id]
            INNER JOIN
                [Rounds] r ON ubu.[RoundId] = r.[Id]
            LEFT JOIN
                [LeagueRoundResults] lrr
                    ON lrr.[LeagueId] = ubu.[LeagueId]
                    AND lrr.[RoundId] = ubu.[RoundId]
                    AND lrr.[UserId] = ubu.[UserId]
            WHERE
                ubu.[LeagueId] = @LeagueId
                AND ubu.[SeasonId] = @SeasonId;";

        return (await dbConnection.QueryAsync<BoostUsageRow>(
            sql, cancellationToken, new { LeagueId = leagueId, SeasonId = seasonId })).ToList();
    }

    private async Task<BoostRoundRangeRow?> GetRoundRangeAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                MIN(r.[RoundNumber]) AS [MinRoundNumber],
                MAX(r.[RoundNumber]) AS [MaxRoundNumber]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON r.[SeasonId] = l.[SeasonId]
            WHERE
                l.[Id] = @LeagueId
            HAVING
                COUNT(*) > 0;";

        return await dbConnection.QuerySingleOrDefaultAsync<BoostRoundRangeRow>(
            sql, cancellationToken, new { LeagueId = leagueId });
    }

    private async Task<int?> GetRoundNumberByStatusAsync(
        int leagueId, RoundStatus status, bool ascending, CancellationToken cancellationToken)
    {
        // TOP 1 with an ORDER BY is selection, not a rule: it picks which single row to fetch. The two
        // directions are the earliest in-progress round and the latest completed one.
        var sql = $@"
            SELECT TOP 1
                r.[RoundNumber]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON r.[SeasonId] = l.[SeasonId]
            WHERE
                l.[Id] = @LeagueId
                AND r.[Status] = @Status
            ORDER BY
                r.[RoundNumber] {(ascending ? "ASC" : "DESC")};";

        return await dbConnection.QuerySingleOrDefaultAsync<int?>(
            sql, cancellationToken, new { LeagueId = leagueId, Status = status.ToString() });
    }
}
