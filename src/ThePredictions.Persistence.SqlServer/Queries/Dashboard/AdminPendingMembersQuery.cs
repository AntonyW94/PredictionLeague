using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Dashboard;

/// <summary>
/// The SQL Server reads behind <see cref="IAdminPendingMembersQuery"/>.
///
/// Every league the player runs and every pending request to one of them. Neither read filters by whether a league is still
/// taking entries: that comparison used <c>GETUTCDATE()</c> and is now a rule against an injected clock, applied once by the
/// handler instead of repeated in both statements.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class AdminPendingMembersQuery(IApplicationReadDbConnection dbConnection) : IAdminPendingMembersQuery
{
    public async Task<AdminPendingMembersData> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        var leaguesTask = GetLeaguesAsync(userId, cancellationToken);
        var membersTask = GetPendingMembersAsync(userId, cancellationToken);

        await Task.WhenAll(leaguesTask, membersTask);

        return new AdminPendingMembersData(leaguesTask.Result, membersTask.Result);
    }

    private async Task<IReadOnlyList<AdministeredLeagueRow>> GetLeaguesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[EntryDeadlineUtc],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [MemberCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @PendingStatus
                ) AS [PendingCount],
                l.[Price],
                l.[IsFree],
                l.[EntryCode]
            FROM
                [Leagues] l
            WHERE
                l.[AdministratorUserId] = @UserId;";

        return (await dbConnection.QueryAsync<AdministeredLeagueRow>(
            sql, cancellationToken,
            new
            {
                UserId = userId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                PendingStatus = nameof(LeagueMemberStatus.Pending)
            })).ToList();
    }

    private async Task<IReadOnlyList<PendingMemberRow>> GetPendingMembersAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                lm.[UserId],
                u.[FirstName],
                u.[LastName],
                lm.[JoinedAtUtc]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                l.[AdministratorUserId] = @UserId
                AND lm.[Status] = @PendingStatus;";

        return (await dbConnection.QueryAsync<PendingMemberRow>(
            sql, cancellationToken,
            new { UserId = userId, PendingStatus = nameof(LeagueMemberStatus.Pending) })).ToList();
    }
}
