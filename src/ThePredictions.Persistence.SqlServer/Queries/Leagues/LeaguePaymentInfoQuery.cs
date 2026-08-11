using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="ILeaguePaymentInfoQuery"/>.
///
/// Returns the caller's standing in the league as two facts and the bank details as stored ciphertext. It decides
/// nothing: who may see the details, and whether an entry code matches, are rules in the handler.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeaguePaymentInfoQuery(IApplicationReadDbConnection dbConnection) : ILeaguePaymentInfoQuery
{
    public async Task<LeaguePaymentInfoRow?> ExecuteAsync(
        int leagueId,
        string requestingUserId,
        CancellationToken cancellationToken)
    {
        // The caller's name is joined in because it becomes their payment reference when the league has not set one.
        // A left join rather than the old cross join, so an unknown caller cannot make an existing league look missing.
        const string sql = @"
            SELECT
                l.[Name] AS [LeagueName],
                l.[Price],
                l.[EntryCode],
                l.[BankAccountName] AS [EncryptedAccountName],
                l.[BankSortCode] AS [EncryptedSortCode],
                l.[BankAccountNumber] AS [EncryptedAccountNumber],
                l.[PaymentReferenceTemplate],
                CAST(CASE WHEN l.[AdministratorUserId] = @UserId THEN 1 ELSE 0 END AS bit) AS [IsAdministrator],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[UserId] = @UserId
                ) THEN 1 ELSE 0 END AS bit) AS [HasMembership],
                u.[FirstName] AS [RequestingFirstName],
                u.[LastName] AS [RequestingLastName]
            FROM
                [Leagues] l
            LEFT JOIN
                [AspNetUsers] u ON u.[Id] = @UserId
            WHERE
                l.[Id] = @LeagueId;";

        return await dbConnection.QuerySingleOrDefaultAsync<LeaguePaymentInfoRow>(
            sql, cancellationToken, new { LeagueId = leagueId, UserId = requestingUserId });
    }
}
