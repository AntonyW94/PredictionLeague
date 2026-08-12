using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeagueBankDetailsQueryHandler(ILeagueBankDetailsQuery bankDetailsQuery, IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetLeagueBankDetailsQuery, LeagueBankDetailsDto>
{
    public async Task<LeagueBankDetailsDto> Handle(GetLeagueBankDetailsQuery request, CancellationToken cancellationToken)
    {
        var row = await bankDetailsQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (row is null)
            throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");

        if (row.AdministratorUserId != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the league administrator can view its bank details.");

        return new LeagueBankDetailsDto(
            fieldEncryptionService.Decrypt(row.EncryptedAccountName),
            fieldEncryptionService.Decrypt(row.EncryptedSortCode),
            fieldEncryptionService.Decrypt(row.EncryptedAccountNumber),
            row.PaymentReferenceTemplate);
    }
}
