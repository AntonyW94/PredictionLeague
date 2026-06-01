using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Account.Commands;

public class SetPayoutDetailsCommandHandler(
    IUserPayoutDetailsRepository payoutDetailsRepository,
    IFieldEncryptionService fieldEncryptionService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<SetPayoutDetailsCommand>
{
    public async Task Handle(SetPayoutDetailsCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);

        var encryptedAccountName = fieldEncryptionService.Encrypt(NullIfBlank(request.AccountName));
        var encryptedSortCode = fieldEncryptionService.Encrypt(NullIfBlank(request.SortCode));
        var encryptedAccountNumber = fieldEncryptionService.Encrypt(NullIfBlank(request.AccountNumber));

        var existing = await payoutDetailsRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        UserPayoutDetails details;
        if (existing is null)
        {
            details = UserPayoutDetails.Create(request.UserId, encryptedAccountName, encryptedSortCode, encryptedAccountNumber, dateTimeProvider);
        }
        else
        {
            existing.Update(encryptedAccountName, encryptedSortCode, encryptedAccountNumber, dateTimeProvider);
            details = existing;
        }

        await payoutDetailsRepository.UpsertAsync(details, cancellationToken);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
