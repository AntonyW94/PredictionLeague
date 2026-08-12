using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>The SQL Server read behind <see cref="ILeagueBankDetailsQuery"/>.</summary>
/// <remarks>
/// The administrator's id is selected alongside the ciphertext so the handler can refuse the request before decrypting anything.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueBankDetailsQuery(IApplicationReadDbConnection dbConnection) : ILeagueBankDetailsQuery
{
    public async Task<EncryptedLeagueBankDetailsRow?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[AdministratorUserId],
                l.[BankAccountName] AS [EncryptedAccountName],
                l.[BankSortCode] AS [EncryptedSortCode],
                l.[BankAccountNumber] AS [EncryptedAccountNumber],
                l.[PaymentReferenceTemplate]
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;";

        return await dbConnection.QuerySingleOrDefaultAsync<EncryptedLeagueBankDetailsRow>(
            sql, cancellationToken, new { LeagueId = leagueId });
    }
}
