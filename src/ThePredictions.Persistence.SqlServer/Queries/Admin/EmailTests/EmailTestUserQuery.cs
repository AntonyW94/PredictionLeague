using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Application.Services;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.EmailTests;

/// <summary>The SQL Server read behind <see cref="IEmailTestUserQuery"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class EmailTestUserQuery(IApplicationReadDbConnection dbConnection) : IEmailTestUserQuery
{
    public async Task<EmailTestUserData?> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[FirstName],
                u.[LastName],
                u.[Email]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Id] = @UserId;";

        return await dbConnection.QuerySingleOrDefaultAsync<EmailTestUserData>(sql, cancellationToken, new { UserId = userId });
    }
}
