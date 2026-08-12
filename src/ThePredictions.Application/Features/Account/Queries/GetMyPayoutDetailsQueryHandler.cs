using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>A player's own bank details, and who would be paying them prize money.</summary>
public class GetMyPayoutDetailsQueryHandler(
    IMyPayoutDetailsQuery payoutDetailsQuery,
    IFieldEncryptionService fieldEncryptionService) : IRequestHandler<GetMyPayoutDetailsQuery, MyPayoutDetailsDto>
{
    public async Task<MyPayoutDetailsDto> Handle(GetMyPayoutDetailsQuery request, CancellationToken cancellationToken)
    {
        var row = await payoutDetailsQuery.GetDetailsAsync(request.UserId, cancellationToken);
        var administrators = await payoutDetailsQuery.GetPayingAdministratorsAsync(request.UserId, cancellationToken);

        var accountName = fieldEncryptionService.Decrypt(row?.EncryptedAccountName);
        var sortCode = fieldEncryptionService.Decrypt(row?.EncryptedSortCode);
        var accountNumber = fieldEncryptionService.Decrypt(row?.EncryptedAccountNumber);

        return new MyPayoutDetailsDto(
            accountName,
            sortCode,
            accountNumber,
            BankDetails.AreComplete(accountName, sortCode, accountNumber),
            AdministratorNames(administrators));
    }

    /// <summary>
    /// The administrators who pay prizes in this player's leagues, named in full and in alphabetical order.
    /// </summary>
    /// <remarks>
    /// The full name rather than the abbreviated "Ada L" every other screen shows, because this one is telling somebody who to
    /// expect money from. The same person administering two of their leagues is named once.
    /// </remarks>
    private static List<string> AdministratorNames(IEnumerable<PayingAdministratorRow> administrators) =>
        administrators
            .Select(administrator => PlayerDisplayName.FormatFull(administrator.FirstName, administrator.LastName))
            .Distinct()
            .OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
}
