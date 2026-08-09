using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeaguePayoutsQueryHandler(IApplicationReadDbConnection dbConnection, IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetLeaguePayoutsQuery, LeaguePayoutsDto>
{
    public async Task<LeaguePayoutsDto> Handle(GetLeaguePayoutsQuery request, CancellationToken cancellationToken)
    {
        const string leagueSql = @"
            SELECT
                l.[AdministratorUserId],
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [Rounds] r WHERE r.[SeasonId] = l.[SeasonId])
                          AND NOT EXISTS (SELECT 1 FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] <> @CompletedStatus)
                     THEN 1 ELSE 0 END AS BIT) AS SeasonComplete
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;";

        var league = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(
            leagueSql,
            cancellationToken,
            new { request.LeagueId, CompletedStatus = nameof(RoundStatus.Completed) });

        if (league is null)
            throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");

        if (league.AdministratorUserId != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the league administrator can view payouts.");

        const string winningsSql = @"
            SELECT
                w.[UserId],
                u.[FirstName] + ' ' + u.[LastName] AS UserName,
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

        var winningRows = (await dbConnection.QueryAsync<WinningRow>(winningsSql, cancellationToken, new { request.LeagueId })).ToList();

        const string payoutsSql = @"
            SELECT
                [UserId],
                [TotalAmount],
                [PaidAtUtc]
            FROM
                [LeaguePayouts]
            WHERE
                [LeagueId] = @LeagueId;";

        var storedByUser = (await dbConnection.QueryAsync<StoredPayoutRow>(payoutsSql, cancellationToken, new { request.LeagueId }))
            .ToDictionary(p => p.UserId);

        var winnerUserIds = winningRows.Select(w => w.UserId).Distinct().ToArray();

        var detailsByUser = new Dictionary<string, PayoutDetailRow>();
        if (winnerUserIds.Length > 0)
        {
            const string detailsSql = @"
                SELECT
                    [UserId],
                    [AccountName],
                    [SortCode],
                    [AccountNumber]
                FROM
                    [UserPayoutDetails]
                WHERE
                    [UserId] IN @UserIds;";

            detailsByUser = (await dbConnection.QueryAsync<PayoutDetailRow>(detailsSql, cancellationToken, new { UserIds = winnerUserIds }))
                .ToDictionary(d => d.UserId);
        }

        var winners = winningRows
            .GroupBy(w => new { w.UserId, w.UserName })
            .Select(group =>
            {
                var liveTotal = group.Sum(x => x.Amount);

                var breakdown = group
                    .GroupBy(x => x.PrizeType)
                    .Select(typeGroup => new PayoutBreakdownDto(PrizeCategoryRegistry.Definition(typeGroup.Key).DisplayName, typeGroup.Sum(x => x.Amount)))
                    .OrderBy(b => b.PrizeType)
                    .ToList();

                var stored = storedByUser.GetValueOrDefault(group.Key.UserId);
                var isPaid = stored?.PaidAtUtc is not null;
                var hasDiscrepancy = isPaid && stored!.TotalAmount != liveTotal;

                var details = detailsByUser.GetValueOrDefault(group.Key.UserId);
                var accountName = fieldEncryptionService.Decrypt(details?.AccountName);
                var sortCode = fieldEncryptionService.Decrypt(details?.SortCode);
                var accountNumber = fieldEncryptionService.Decrypt(details?.AccountNumber);
                var hasSharedDetails = accountName is not null && sortCode is not null && accountNumber is not null;

                return new LeaguePayoutWinnerDto(
                    group.Key.UserId,
                    group.Key.UserName,
                    liveTotal,
                    breakdown,
                    isPaid,
                    stored?.PaidAtUtc,
                    hasDiscrepancy,
                    hasSharedDetails,
                    accountName,
                    sortCode,
                    accountNumber);
            })
            .OrderByDescending(w => w.TotalAmount)
            .ThenBy(w => w.UserName)
            .ToList();

        var paidTotal = winners.Where(w => w.IsPaid).Sum(w => storedByUser[w.UserId].TotalAmount);
        var outstandingTotal = winners.Where(w => !w.IsPaid).Sum(w => w.TotalAmount);

        return new LeaguePayoutsDto(league.SeasonComplete, outstandingTotal, paidTotal, winners);
    }

    // internal so a test can supply rows for the shaping above; InternalsVisibleTo already exposes
    // this assembly to ThePredictions.Application.Tests.Unit.
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed record LeagueRow(string AdministratorUserId, bool SeasonComplete);

    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed record WinningRow(string UserId, string UserName, PrizeType PrizeType, decimal Amount);

    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed record StoredPayoutRow(string UserId, decimal TotalAmount, DateTime? PaidAtUtc);

    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed record PayoutDetailRow(string UserId, string? AccountName, string? SortCode, string? AccountNumber);
}
