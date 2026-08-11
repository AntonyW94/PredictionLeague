using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeaguePayoutsQuery"/>.
///
/// Four reads, none of which totals anything, decides whether a season is over, or judges whether a payment matches what
/// is owed. The bank details are still fetched only for the league's winners, which the old handler achieved with an
/// <c>IN</c> clause built from the rows it had already read - a join does it here, so a league with no winners needs no
/// special case.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeaguePayoutsQuery(IApplicationReadDbConnection dbConnection) : ILeaguePayoutsQuery
{
    public async Task<LeaguePayoutsData?> ExecuteAsync(
        int leagueId,
        string requestingUserId,
        CancellationToken cancellationToken)
    {
        var league = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(
            @"
            SELECT
                CAST(CASE WHEN l.[AdministratorUserId] = @UserId THEN 1 ELSE 0 END AS bit) AS [IsAdministrator],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Rounds] r
                    WHERE
                        r.[SeasonId] = l.[SeasonId]
                ) AS [SeasonRoundCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Rounds] r
                    WHERE
                        r.[SeasonId] = l.[SeasonId]
                        AND r.[Status] = @CompletedStatus
                ) AS [CompletedRoundCount]
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;",
            cancellationToken,
            new
            {
                LeagueId = leagueId,
                UserId = requestingUserId,
                CompletedStatus = nameof(RoundStatus.Completed)
            });

        if (league == null)
            return null;

        var winningsTask = GetWinningsAsync(leagueId, cancellationToken);
        var storedTask = GetStoredPayoutsAsync(leagueId, cancellationToken);
        var detailsTask = GetBankDetailsAsync(leagueId, cancellationToken);

        await Task.WhenAll(winningsTask, storedTask, detailsTask);

        return new LeaguePayoutsData(
            league.IsAdministrator,
            league.SeasonRoundCount,
            league.CompletedRoundCount,
            winningsTask.Result,
            storedTask.Result,
            detailsTask.Result);
    }

    private async Task<IReadOnlyList<PayoutWinningRow>> GetWinningsAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                w.[UserId],
                u.[FirstName],
                u.[LastName],
                lps.[PrizeType],
                w.[Amount]
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = w.[UserId]
            WHERE
                lps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<PayoutWinningRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<StoredPayoutRow>> GetStoredPayoutsAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lp.[UserId],
                lp.[TotalAmount],
                lp.[PaidAtUtc]
            FROM
                [LeaguePayouts] lp
            WHERE
                lp.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<StoredPayoutRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<PayoutBankDetailsRow>> GetBankDetailsAsync(int leagueId, CancellationToken cancellationToken)
    {
        // Scoped to this league's winners. Bank details are the most sensitive rows in the schema, so the narrowing is
        // the point of the join rather than an optimisation: nobody else's arrive at all.
        const string sql = @"
            SELECT DISTINCT
                d.[UserId],
                d.[AccountName] AS [EncryptedAccountName],
                d.[SortCode] AS [EncryptedSortCode],
                d.[AccountNumber] AS [EncryptedAccountNumber]
            FROM
                [UserPayoutDetails] d
            INNER JOIN
                [Winnings] w ON w.[UserId] = d.[UserId]
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
            WHERE
                lps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<PayoutBankDetailsRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    // Column order matches the SELECT above, per the Dapper result-mapping rule in CLAUDE.md.
    private sealed record LeagueRow(bool IsAdministrator, int SeasonRoundCount, int CompletedRoundCount);
}
