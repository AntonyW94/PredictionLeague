using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="IOverallLeaderboardQuery"/>.
///
/// Scoping only. No <c>RANK()</c>, no <c>SUM</c>, no <c>GROUP BY</c>, no name concatenation and no
/// <c>ORDER BY</c> - all of those were rules, and they are C# now. What is left is three narrow reads, which
/// is also why this no longer needs the <c>LEFT JOIN</c> and grouping that made the previous single statement
/// hard to read.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class OverallLeaderboardQuery(IApplicationReadDbConnection dbConnection) : IOverallLeaderboardQuery
{
    public async Task<OverallLeaderboardData> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var membersTask = GetMembersAsync(leagueId, cancellationToken);
        var pointsTask = GetRoundPointsAsync(leagueId, cancellationToken);
        var completedTask = HasRoundWithStatusAsync(leagueId, RoundStatus.Completed, cancellationToken);
        var inProgressTask = HasRoundWithStatusAsync(leagueId, RoundStatus.InProgress, cancellationToken);

        await Task.WhenAll(membersTask, pointsTask, completedTask, inProgressTask);

        return new OverallLeaderboardData(
            membersTask.Result, pointsTask.Result, completedTask.Result, inProgressTask.Result);
    }

    private async Task<IReadOnlyList<LeaderboardMemberRow>> GetMembersAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName],
                stats.[SnapshotOverallRank] AS [SnapshotRank]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN
                [LeagueMemberStats] stats ON lm.[LeagueId] = stats.[LeagueId] AND lm.[UserId] = stats.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<LeaderboardMemberRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<MemberRoundPointsRow>> GetRoundPointsAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        // One row per member per scored round, unaggregated. The largest league in production has fewer than
        // 700 of these, so summing them in C# costs nothing and keeps the total's definition in one place.
        const string sql = @"
            SELECT
                lrr.[UserId],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            WHERE
                lrr.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<MemberRoundPointsRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<bool> HasRoundWithStatusAsync(
        int leagueId, RoundStatus status, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Rounds] r
                    INNER JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @Status
                ) THEN 1 ELSE 0 END AS bit);";

        return await dbConnection.QuerySingleOrDefaultAsync<bool>(
            sql, cancellationToken, new { LeagueId = leagueId, Status = status.ToString() });
    }
}
