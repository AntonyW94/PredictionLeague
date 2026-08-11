using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="IMonthlyLeaderboardQuery"/>.
///
/// Scoping only. <c>MONTH([StartDateUtc]) = @Month</c> stays here because selecting which rounds fall in the
/// month is choosing rows, not interpreting them - and it is matched within the league's season, so the absence
/// of a year check is safe for as long as a season never spans the same month twice.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class MonthlyLeaderboardQuery(IApplicationReadDbConnection dbConnection) : IMonthlyLeaderboardQuery
{
    public async Task<MonthlyLeaderboardData> ExecuteAsync(
        int leagueId, int month, CancellationToken cancellationToken)
    {
        var membersTask = GetMembersAsync(leagueId, cancellationToken);
        var pointsTask = GetMonthPointsAsync(leagueId, month, cancellationToken);
        var statusesTask = GetMonthRoundStatusesAsync(leagueId, month, cancellationToken);
        var inProgressTask = HasRoundInProgressAsync(leagueId, cancellationToken);

        await Task.WhenAll(membersTask, pointsTask, statusesTask, inProgressTask);

        return new MonthlyLeaderboardData(
            membersTask.Result, pointsTask.Result, statusesTask.Result, inProgressTask.Result);
    }

    private async Task<IReadOnlyList<LeaderboardMemberRow>> GetMembersAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName],
                stats.[SnapshotMonthRank] AS [SnapshotRank]
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

    private async Task<IReadOnlyList<MemberRoundPointsRow>> GetMonthPointsAsync(
        int leagueId, int month, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lrr.[UserId],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            INNER JOIN
                [Rounds] r ON r.[Id] = lrr.[RoundId]
            INNER JOIN
                [Leagues] l ON l.[Id] = lrr.[LeagueId] AND l.[SeasonId] = r.[SeasonId]
            WHERE
                lrr.[LeagueId] = @LeagueId
                AND MONTH(r.[StartDateUtc]) = @Month;";

        return (await dbConnection.QueryAsync<MemberRoundPointsRow>(
            sql, cancellationToken, new { LeagueId = leagueId, Month = month })).ToList();
    }

    private async Task<IReadOnlyList<RoundStatus>> GetMonthRoundStatusesAsync(
        int leagueId, int month, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Status]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            WHERE
                l.[Id] = @LeagueId
                AND MONTH(r.[StartDateUtc]) = @Month;";

        var statuses = await dbConnection.QueryAsync<string>(
            sql, cancellationToken, new { LeagueId = leagueId, Month = month });

        return statuses.Select(Enum.Parse<RoundStatus>).ToList();
    }

    private async Task<bool> HasRoundInProgressAsync(int leagueId, CancellationToken cancellationToken)
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
            sql, cancellationToken,
            new { LeagueId = leagueId, Status = nameof(RoundStatus.InProgress) });
    }
}
