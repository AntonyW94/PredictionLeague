using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;

namespace ThePredictions.Persistence.SqlServer.Queries;

/// <summary>
/// The SQL Server read behind <see cref="IEmailSettingsQuery"/>.
///
/// The last SQL statement in Infrastructure, which is now free of it.
/// </summary>
/// <remarks>
/// <c>EmailSettings</c> is a single-row table, and the lowest id is the row: an ordered <c>TOP 1</c> rather than an
/// aggregate, so that a second row appearing by accident cannot change the answer at random.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class EmailSettingsQuery(IApplicationReadDbConnection dbConnection) : IEmailSettingsQuery
{
    public async Task<bool?> GetEmailsEnabledAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1
                es.[EmailsEnabled]
            FROM
                [EmailSettings] es
            ORDER BY
                es.[Id];";

        return await dbConnection.QuerySingleOrDefaultAsync<bool?>(sql, cancellationToken);
    }
}
