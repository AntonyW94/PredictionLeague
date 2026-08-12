using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Account.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Account;

/// <summary>
/// The SQL Server read behind <see cref="IAccountProfileQuery"/>. The <c>CASE WHEN [MarketingOptInAtUtc] IS NOT NULL</c> is
/// gone: the date comes back and reading it as consent is a rule.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class AccountProfileQuery(IApplicationReadDbConnection dbConnection) : IAccountProfileQuery
{
    public async Task<AccountProfileRow?> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[FirstName],
                u.[LastName],
                u.[Email],
                u.[PhoneNumber],
                u.[PreferredTheme],
                u.[MarketingOptInAtUtc]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Id] = @UserId;";

        return await dbConnection.QuerySingleOrDefaultAsync<AccountProfileRow>(sql, cancellationToken, new { UserId = userId });
    }
}
