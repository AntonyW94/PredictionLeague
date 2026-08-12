using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
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
