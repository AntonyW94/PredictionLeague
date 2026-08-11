using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Dashboard;

/// <summary>
/// The SQL Server read behind <see cref="IJoinableLeaguesQuery"/>.
///
/// One statement replacing two that filtered differently. The only predicate left is the scoping one - leagues the player
/// has no membership row for - because the rest were rules: whether a league is findable, whether it is still open, and
/// whether the player holds a pass for its season.
/// </summary>
/// <remarks>
/// The entry code is reported as a flag rather than returned. These are leagues the player is not a member of, so the code
/// itself must not travel - only the fact that there is one.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class JoinableLeaguesQuery(IApplicationReadDbConnection dbConnection) : IJoinableLeaguesQuery
{
    public async Task<IReadOnlyList<JoinableLeagueRow>> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name],
                s.[Name] AS [SeasonName],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                l.[Price],
                l.[PrizeFundOverride],
                l.[EntryDeadlineUtc],
                CAST(CASE WHEN l.[EntryCode] IS NOT NULL THEN 1 ELSE 0 END AS bit) AS [HasEntryCode],
                l.[IsListed],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [MemberCount],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [SeasonPasses] sp
                    WHERE
                        sp.[UserId] = @UserId
                        AND sp.[SeasonId] = l.[SeasonId]
                ) THEN 1 ELSE 0 END AS bit) AS [HasSeasonPass]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            WHERE
                NOT EXISTS (
                    SELECT
                        1
                    FROM
                        [LeagueMembers] mine
                    WHERE
                        mine.[LeagueId] = l.[Id]
                        AND mine.[UserId] = @UserId
                );";

        return (await dbConnection.QueryAsync<JoinableLeagueRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}
