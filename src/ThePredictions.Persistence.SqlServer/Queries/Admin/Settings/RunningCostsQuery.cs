using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Settings;

/// <summary>The SQL Server read behind <see cref="IRunningCostsQuery"/>. Its <c>ORDER BY [Name]</c> was a rule and has moved.</summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class RunningCostsQuery(IApplicationReadDbConnection dbConnection) : IRunningCostsQuery
{
    public async Task<IReadOnlyList<RunningCostRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                rc.[Id],
                rc.[Name],
                rc.[Amount],
                rc.[Frequency],
                rc.[StartDateUtc],
                rc.[EndDateUtc],
                rc.[Notes]
            FROM
                [RunningCosts] rc;";

        return (await dbConnection.QueryAsync<RunningCostRow>(sql, cancellationToken)).ToList();
    }
}
