using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Sharing.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Sharing;

/// <summary>The SQL Server read behind <see cref="IShareCardPlayerQuery"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class ShareCardPlayerQuery(IApplicationReadDbConnection dbConnection) : IShareCardPlayerQuery
{
    public async Task<ShareCardPlayerRow?> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[FirstName],
                u.[PreferredTheme]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Id] = @UserId;";

        return await dbConnection.QuerySingleOrDefaultAsync<ShareCardPlayerRow>(sql, cancellationToken, new { UserId = userId });
    }
}
