using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Domain.Services;
using ThePredictions.Domain.Services.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's payout screen: who is owed what, who has been paid, and the bank details to pay them with.
/// </summary>
public class GetLeaguePayoutsQueryHandler(
    ILeaguePayoutsQuery payoutsQuery,
    IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetLeaguePayoutsQuery, LeaguePayoutsDto>
{
    public async Task<LeaguePayoutsDto> Handle(GetLeaguePayoutsQuery request, CancellationToken cancellationToken)
    {
        var data = await payoutsQuery.ExecuteAsync(request.LeagueId, request.RequestingUserId, cancellationToken);

        if (data is null)
            throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");

        // Only the administrator pays anybody, so only the administrator sees who is owed what - or their bank details.
        // Its own message rather than the shared membership service's, because this screen names what it is refusing.
        if (!data.IsAdministrator)
            throw new UnauthorizedAccessException("Only the league administrator can view payouts.");

        var storedByUser = data.StoredPayouts.ToDictionary(payout => payout.UserId);
        var bankDetailsByUser = data.BankDetails.ToDictionary(details => details.UserId);

        var winners = data.Winnings
            .GroupBy(winning => winning.UserId)
            .Select(group => ToWinner(group.Key, group.ToList(), storedByUser, bankDetailsByUser))
            .OrderByDescending(winner => winner.TotalAmount)
            .ThenBy(winner => winner.UserName, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        return new LeaguePayoutsDto(
            SeasonCompletion.IsEveryRoundComplete(data.SeasonRoundCount, data.CompletedRoundCount),
            OutstandingTotal(winners),
            PaidTotal(winners, storedByUser),
            winners);
    }

    private LeaguePayoutWinnerDto ToWinner(
        string userId,
        IReadOnlyList<PayoutWinningRow> winnings,
        IReadOnlyDictionary<string, StoredPayoutRow> storedByUser,
        IReadOnlyDictionary<string, PayoutBankDetailsRow> bankDetailsByUser)
    {
        var liveTotal = winnings.Sum(winning => winning.Amount);

        var stored = storedByUser.GetValueOrDefault(userId);
        var isPaid = stored?.PaidAtUtc is not null;

        var details = bankDetailsByUser.GetValueOrDefault(userId);
        var accountName = fieldEncryptionService.Decrypt(details?.EncryptedAccountName);
        var sortCode = fieldEncryptionService.Decrypt(details?.EncryptedSortCode);
        var accountNumber = fieldEncryptionService.Decrypt(details?.EncryptedAccountNumber);

        return new LeaguePayoutWinnerDto(
            userId,
            PlayerDisplayName.FormatFull(winnings[0].FirstName, winnings[0].LastName),
            liveTotal,
            BreakdownOf(winnings),
            isPaid,
            stored?.PaidAtUtc,
            HasDiscrepancy(isPaid, stored, liveTotal),
            BankDetails.AreComplete(accountName, sortCode, accountNumber),
            accountName,
            sortCode,
            accountNumber);
    }

    /// <summary>
    /// What a winner is owed, split by the kind of prize it came from.
    /// </summary>
    private static List<PayoutBreakdownDto> BreakdownOf(IEnumerable<PayoutWinningRow> winnings) =>
        winnings
            .GroupBy(winning => winning.PrizeType)
            .Select(group => new PayoutBreakdownDto(
                PrizeCategoryRegistry.Definition(group.Key).DisplayName,
                group.Sum(winning => winning.Amount)))
            .OrderBy(breakdown => breakdown.PrizeType, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Whether what was paid no longer matches what is owed - which happens when a round is re-processed and prizes move
    /// after somebody has already been paid.
    /// </summary>
    /// <remarks>
    /// Only meaningful once a payment has been recorded: an unpaid winner whose total has changed is simply owed the new
    /// figure, and flagging that would put a warning on every screen mid-season.
    /// </remarks>
    private static bool HasDiscrepancy(bool isPaid, StoredPayoutRow? stored, decimal liveTotal)
    {
        if (!isPaid)
            return false;

        return stored!.TotalAmount != liveTotal;
    }

    /// <summary>
    /// What is still owed: the live totals of everyone unpaid.
    /// </summary>
    private static decimal OutstandingTotal(IEnumerable<LeaguePayoutWinnerDto> winners) =>
        winners.Where(winner => !winner.IsPaid).Sum(winner => winner.TotalAmount);

    /// <summary>
    /// What has been paid out - the <b>recorded</b> amounts, not the current ones.
    /// </summary>
    /// <remarks>
    /// Deliberately different from <see cref="OutstandingTotal"/>, which uses live totals. Money that has left the
    /// administrator's account is a historical fact, so re-pricing a prize afterwards must not change what the screen says
    /// was paid. That difference is what makes a discrepancy visible rather than silently absorbed.
    /// </remarks>
    private static decimal PaidTotal(
        IEnumerable<LeaguePayoutWinnerDto> winners,
        IReadOnlyDictionary<string, StoredPayoutRow> storedByUser) =>
        winners
            .Where(winner => winner.IsPaid)
            .Sum(winner => storedByUser[winner.UserId].TotalAmount);
}
