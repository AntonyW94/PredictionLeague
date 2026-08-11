using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Settings;

/// <summary>
/// The SQL Server read behind <see cref="IPricingSettingsQuery"/>. The <c>TOP 1 ORDER BY [Id]</c> is gone: which row
/// counts as the live one is a decision, and the id comes back so C# can make it.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class PricingSettingsQuery(IApplicationReadDbConnection dbConnection) : IPricingSettingsQuery
{
    public async Task<IReadOnlyList<PricingSettingsRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ps.[Id],
                ps.[BufferRate],
                ps.[MinimumFloor]
            FROM
                [PricingSettings] ps;";

        return (await dbConnection.QueryAsync<PricingSettingsRow>(sql, cancellationToken)).ToList();
    }
}
