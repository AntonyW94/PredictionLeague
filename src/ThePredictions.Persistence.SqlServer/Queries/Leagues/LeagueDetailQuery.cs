using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="ILeagueDetailQuery"/>.
///
/// The columns as stored, with no <c>ISNULL</c> standing in for a display value and no <c>CASE</c> turning a
/// competition type into a flag. The <c>GROUP BY</c> that listed thirteen columns has gone with the aggregate that
/// required it - the two member counts are subqueries, so nothing needs grouping.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueDetailQuery(IApplicationReadDbConnection dbConnection) : ILeagueDetailQuery
{
    public async Task<LeagueDetailRow?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS [SeasonName],
                l.[SeasonId],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                ) AS [TotalMembershipCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [ApprovedMemberCount],
                l.[Price],
                l.[EntryCode],
                l.[EntryDeadlineUtc],
                l.[PointsForExactScore],
                l.[PointsForCorrectResult],
                c.[Type] AS [CompetitionType],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [LeaguePrizeScheme] lps
                    WHERE
                        lps.[LeagueId] = l.[Id]
                ) THEN 1 ELSE 0 END AS bit) AS [HasPrizeScheme],
                l.[RequiresMemberApproval],
                l.[IsListed]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                l.[Id] = @LeagueId;";

        return await dbConnection.QuerySingleOrDefaultAsync<LeagueDetailRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }
}
