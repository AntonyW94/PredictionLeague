using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeagueBankDetailsQueryHandler(IApplicationReadDbConnection dbConnection, IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetLeagueBankDetailsQuery, LeagueBankDetailsDto>
{
    public async Task<LeagueBankDetailsDto> Handle(GetLeagueBankDetailsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[AdministratorUserId],
                l.[BankAccountName] AS EncryptedAccountName,
                l.[BankSortCode] AS EncryptedSortCode,
                l.[BankAccountNumber] AS EncryptedAccountNumber,
                l.[PaymentReferenceTemplate]
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;";

        var row = await dbConnection.QuerySingleOrDefaultAsync<BankDetailsRow>(
            sql,
            cancellationToken,
            new { request.LeagueId });

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

    private sealed record BankDetailsRow(
        string AdministratorUserId,
        string? EncryptedAccountName,
        string? EncryptedSortCode,
        string? EncryptedAccountNumber,
        string? PaymentReferenceTemplate);
}
