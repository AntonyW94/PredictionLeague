using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Rounds.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Rounds;

/// <summary>
/// The SQL Server read behind <see cref="IAdminRoundQuery"/>.
///
/// What is gone: the second copy of the dead <c>ActiveMemberCount</c> CTE, and the left join to the fixtures that
/// flattened one round across a row per fixture - which is now its own read.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class AdminRoundQuery(IApplicationReadDbConnection dbConnection) : IAdminRoundQuery
{
    public async Task<AdminRoundRow?> ExecuteAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[ApiRoundName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                ) AS [MatchCount]
            FROM
                [Rounds] r
            WHERE
                r.[Id] = @RoundId;";

        return await dbConnection.QuerySingleOrDefaultAsync<AdminRoundRow>(sql, cancellationToken, new { RoundId = roundId });
    }
}
