using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Competitions.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Competitions;

/// <summary>
/// The SQL Server read behind <see cref="ICompetitionsQuery"/>. The <c>ORDER BY</c> and the second statement that picked
/// one competition out by id are both gone.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class CompetitionsQuery(IApplicationReadDbConnection dbConnection) : ICompetitionsQuery
{
    public async Task<IReadOnlyList<CompetitionRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                c.[Id],
                c.[Code],
                c.[Name],
                c.[Type],
                c.[LogoUrl],
                c.[Description],
                c.[ApiLeagueId],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Seasons] s
                    WHERE
                        s.[CompetitionId] = c.[Id]
                ) AS [SeasonCount]
            FROM
                [Competitions] c;";

        return (await dbConnection.QueryAsync<CompetitionRow>(sql, cancellationToken)).ToList();
    }
}
