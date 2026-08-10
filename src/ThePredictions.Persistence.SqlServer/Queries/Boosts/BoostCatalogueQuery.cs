using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Boosts.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Boosts;

/// <summary>
/// The SQL Server read behind <see cref="IBoostCatalogueQuery"/>. No ordering and no shaping: the handler
/// owns both, so this returns the rows and nothing else.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class BoostCatalogueQuery(IApplicationReadDbConnection dbConnection) : IBoostCatalogueQuery
{
    public async Task<IReadOnlyList<BoostCatalogueRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                bd.[Code],
                bd.[Name],
                bd.[Description],
                bd.[Tooltip],
                bd.[Scope],
                bd.[ImageUrl],
                bd.[SelectedImageUrl],
                bd.[DisabledImageUrl]
            FROM
                [BoostDefinitions] bd;";

        return (await dbConnection.QueryAsync<BoostCatalogueRow>(sql, cancellationToken)).ToList();
    }
}
