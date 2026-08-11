using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Settings;

/// <summary>The SQL Server read behind <see cref="IServiceFeesQuery"/>. Its <c>ORDER BY [Provider]</c> was a rule and has moved.</summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class ServiceFeesQuery(IApplicationReadDbConnection dbConnection) : IServiceFeesQuery
{
    public async Task<IReadOnlyList<ServiceFeeRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sf.[Provider],
                sf.[PercentFee],
                sf.[FixedFee]
            FROM
                [ServiceFees] sf;";

        return (await dbConnection.QueryAsync<ServiceFeeRow>(sql, cancellationToken)).ToList();
    }
}
