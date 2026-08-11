using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Dashboard;

/// <summary>
/// The SQL Server reads behind <see cref="IDashboardLeaderboardsQuery"/>.
///
/// Three reads in place of one windowed CTE. Every predicate is scoping - which leagues the player is in, who
/// else is approved in them, which results belong to them. Nothing ranks, sums, names, gates or orders.
///
/// The two aggregates that remain are counts of rounds by status, not judgements: how many of a season's rounds
/// have finished, and whether one is under way. What those numbers mean - whether the season is over, whether a
/// rank-change arrow is worth showing - is decided in C#.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class DashboardLeaderboardsQuery(IApplicationReadDbConnection dbConnection) : IDashboardLeaderboardsQuery
{
    public async Task<DashboardLeaderboardsData> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        var leaguesTask = GetLeaguesAsync(userId, cancellationToken);
        var membersTask = GetMembersAsync(userId, cancellationToken);
        var pointsTask = GetPointsAsync(userId, cancellationToken);

        await Task.WhenAll(leaguesTask, membersTask, pointsTask);

        return new DashboardLeaderboardsData(leaguesTask.Result, membersTask.Result, pointsTask.Result);
    }

    private async Task<IReadOnlyList<DashboardLeagueRow>> GetLeaguesAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[Price],
                s.[Name] AS [SeasonName],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                s.[NumberOfRounds],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Rounds] r
                    WHERE
                        r.[SeasonId] = l.[SeasonId]
                        AND r.[Status] = @CompletedStatus
                ) AS [CompletedRoundCount],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [Rounds] r
                    WHERE
                        r.[SeasonId] = l.[SeasonId]
                        AND r.[Status] = @InProgressStatus
                ) THEN 1 ELSE 0 END AS bit) AS [HasRoundInProgress],
                lm.[IsArchivedByUser]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            WHERE
                lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<DashboardLeagueRow>(
            sql, cancellationToken,
            new
            {
                UserId = userId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed),
                InProgressStatus = nameof(RoundStatus.InProgress)
            })).ToList();
    }

    private async Task<IReadOnlyList<DashboardLeagueMemberRow>> GetMembersAsync(string userId, CancellationToken cancellationToken)
    {
        // Everyone approved in a league the player is approved in - the tile shows whole tables, not just the
        // viewer's own row.
        const string sql = @"
            SELECT
                lm.[LeagueId],
                lm.[UserId],
                u.[FirstName],
                u.[LastName],
                stats.[SnapshotOverallRank] AS [SnapshotRank]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            INNER JOIN
                [LeagueMembers] mine ON mine.[LeagueId] = lm.[LeagueId]
                    AND mine.[UserId] = @UserId
                    AND mine.[Status] = @ApprovedStatus
            LEFT JOIN
                [LeagueMemberStats] stats ON stats.[LeagueId] = lm.[LeagueId] AND stats.[UserId] = lm.[UserId]
            WHERE
                lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<DashboardLeagueMemberRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<DashboardLeagueMemberPointsRow>> GetPointsAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lrr.[LeagueId],
                lrr.[UserId],
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
                    AND mine.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<DashboardLeagueMemberPointsRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}
