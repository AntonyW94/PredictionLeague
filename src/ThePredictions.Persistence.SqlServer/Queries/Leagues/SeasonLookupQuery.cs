using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="ISeasonLookupQuery"/>.
///
/// Every season, unordered, with its competition type rather than a flag. Which ones may host a new league, and in what
/// order they are offered, are rules.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonLookupQuery(IApplicationReadDbConnection dbConnection) : ISeasonLookupQuery
{
    public async Task<IReadOnlyList<SeasonLookupRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDateUtc],
                s.[IsActive],
                c.[Type] AS [CompetitionType]
            FROM
                [Seasons] s
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId];";

        return (await dbConnection.QueryAsync<SeasonLookupRow>(sql, cancellationToken)).ToList();
    }
}
