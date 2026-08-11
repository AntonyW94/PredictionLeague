using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Dashboard;

/// <summary>
/// The SQL Server read behind <see cref="IMyLeagueRequestsQuery"/>.
///
/// Every membership of the player's that is not an approved one. Which of those still belong on their dashboard - and in
/// particular whether a dismissed rejection does - is the handler's rule, so the dismissal flag comes back rather than being
/// filtered on.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class MyLeagueRequestsQuery(IApplicationReadDbConnection dbConnection) : IMyLeagueRequestsQuery
{
    public async Task<IReadOnlyList<MyLeagueRequestRow>> ExecuteAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                s.[Name] AS [SeasonName],
                lm.[Status],
                lm.[IsAlertDismissed],
                lm.[JoinedAtUtc],
                l.[EntryDeadlineUtc],
                u.[FirstName] AS [AdminFirstName],
                u.[LastName] AS [AdminLastName],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] counted
                    WHERE
                        counted.[LeagueId] = l.[Id]
                        AND counted.[Status] = @ApprovedStatus
                ) AS [MemberCount],
                l.[Price],
                l.[PrizeFundOverride]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = l.[AdministratorUserId]
            WHERE
                lm.[UserId] = @UserId
                AND lm.[Status] <> @ApprovedStatus;";

        return (await dbConnection.QueryAsync<MyLeagueRequestRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}
