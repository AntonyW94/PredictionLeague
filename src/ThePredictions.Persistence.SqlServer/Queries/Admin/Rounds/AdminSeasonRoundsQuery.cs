using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Rounds.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Rounds;

/// <summary>
/// The SQL Server read behind <see cref="IAdminSeasonRoundsQuery"/>.
///
/// What is gone: the <c>ActiveMemberCount</c> CTE, which counted the season's approved league members, cross-joined its
/// single row onto every round and was then never selected; and the <c>ORDER BY</c>, which is a rule.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class AdminSeasonRoundsQuery(IApplicationReadDbConnection dbConnection) : IAdminSeasonRoundsQuery
{
    public async Task<IReadOnlyList<AdminRoundRow>> ExecuteAsync(int seasonId, CancellationToken cancellationToken)
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
                r.[SeasonId] = @SeasonId;";

        return (await dbConnection.QueryAsync<AdminRoundRow>(sql, cancellationToken, new { SeasonId = seasonId })).ToList();
    }
}
