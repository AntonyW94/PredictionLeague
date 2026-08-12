using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Account.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Account;

/// <summary>
/// The SQL Server reads behind <see cref="IMyPayoutDetailsQuery"/>. What is gone: the name composition, the
/// <c>SELECT DISTINCT</c> and the <c>ORDER BY</c> over that composed name.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class MyPayoutDetailsQuery(IApplicationReadDbConnection dbConnection) : IMyPayoutDetailsQuery
{
    public async Task<EncryptedPayoutDetailsRow?> GetDetailsAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                upd.[AccountName] AS [EncryptedAccountName],
                upd.[SortCode] AS [EncryptedSortCode],
                upd.[AccountNumber] AS [EncryptedAccountNumber]
            FROM
                [UserPayoutDetails] upd
            WHERE
                upd.[UserId] = @UserId;";

        return await dbConnection.QuerySingleOrDefaultAsync<EncryptedPayoutDetailsRow>(
            sql, cancellationToken, new { UserId = userId });
    }

    /// <remarks>
    /// Scoped to leagues that pay prizes and administered by somebody else, both of which are about which rows to read: a league
    /// with no prizes will never send anybody money, and nobody needs telling they will be paying themselves.
    /// </remarks>
    public async Task<IReadOnlyList<PayingAdministratorRow>> GetPayingAdministratorsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName]
            FROM
                [Leagues] l
            INNER JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = l.[AdministratorUserId]
            WHERE
                lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus
                AND l.[HasPrizes] = 1
                AND l.[AdministratorUserId] <> @UserId;";

        return (await dbConnection.QueryAsync<PayingAdministratorRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}
