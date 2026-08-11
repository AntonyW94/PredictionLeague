using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's bank details, for a member who needs to pay their entry fee.
/// </summary>
/// <remarks>
/// The rule below decides who may read a league's bank account, and until now it had no tests: the handler was excluded
/// from coverage because it "was a SQL string plus a mapping", which was never true of this one.
/// </remarks>
public class GetLeaguePaymentInfoQueryHandler(
    ILeaguePaymentInfoQuery paymentInfoQuery,
    IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetLeaguePaymentInfoQuery, LeaguePaymentInfoDto>
{
    public async Task<LeaguePaymentInfoDto> Handle(
        GetLeaguePaymentInfoQuery request,
        CancellationToken cancellationToken)
    {
        var league = await paymentInfoQuery.ExecuteAsync(request.LeagueId, request.RequestingUserId, cancellationToken);

        if (league is null)
            throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");

        EnsureMayViewPaymentDetails(league, request.EntryCode);

        var accountName = fieldEncryptionService.Decrypt(league.EncryptedAccountName);
        var sortCode = fieldEncryptionService.Decrypt(league.EncryptedSortCode);
        var accountNumber = fieldEncryptionService.Decrypt(league.EncryptedAccountNumber);

        return new LeaguePaymentInfoDto(
            league.LeagueName,
            BankDetails.AreComplete(accountName, sortCode, accountNumber),
            accountName,
            sortCode,
            accountNumber,
            league.Price,
            PaymentReference(league));
    }

    /// <summary>
    /// Who may see a league's bank details: the administrator, anyone with a membership row, and a prospective joiner
    /// holding the right entry code.
    /// </summary>
    /// <remarks>
    /// "Anyone with a membership row" means any status, including pending - which is the point, because someone who has
    /// asked to join needs the details in order to pay. It also covers somebody who was turned away, which is the part
    /// worth a decision; preserved from the old <c>EXISTS</c>, which had no status filter, and recorded in the plan
    /// document.
    ///
    /// The entry code arm exists so a private league's joining page can show payment details before the request has been
    /// approved. It is compared case-insensitively, as it was, and a blank code supplied by the caller never matches -
    /// otherwise a league with no code at all would be readable by anybody.
    /// </remarks>
    private static void EnsureMayViewPaymentDetails(LeaguePaymentInfoRow league, string? suppliedEntryCode)
    {
        if (league.IsAdministrator || league.HasMembership)
            return;

        if (MatchesEntryCode(league.EntryCode, suppliedEntryCode))
            return;

        throw new UnauthorizedAccessException("Only the league administrator or its members can view payment details.");
    }

    private static bool MatchesEntryCode(string? leagueEntryCode, string? suppliedEntryCode)
    {
        if (string.IsNullOrWhiteSpace(suppliedEntryCode))
            return false;

        return string.Equals(leagueEntryCode, suppliedEntryCode, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What the payer should put as their bank reference: whatever the administrator set for the league, or failing that
    /// the payer's own name, so a reference is never empty.
    /// </summary>
    private static string PaymentReference(LeaguePaymentInfoRow league)
    {
        if (!string.IsNullOrWhiteSpace(league.PaymentReferenceTemplate))
            return league.PaymentReferenceTemplate;

        return $"{league.RequestingFirstName} {league.RequestingLastName}".Trim();
    }
}
