using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="IManageLeaguesQuery"/>. What is gone: the <c>CASE</c> that tagged each league with a
/// category, the <c>ISNULL(l.[EntryCode], 'Public')</c> sentinel, the <c>GROUP BY</c> over ten columns that the member count
/// needed, and the ordering.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class ManageLeaguesQuery(IApplicationReadDbConnection dbConnection) : IManageLeaguesQuery
{
    public async Task<IReadOnlyList<ManageLeagueRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                l.[SeasonId],
                s.[Name] AS [SeasonName],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                l.[AdministratorUserId],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                ) AS [MemberCount],
                l.[Price],
                l.[EntryCode],
                l.[EntryDeadlineUtc],
                l.[PointsForExactScore],
                l.[PointsForCorrectResult]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId];";

        return (await dbConnection.QueryAsync<ManageLeagueRow>(sql, cancellationToken)).ToList();
    }
}
