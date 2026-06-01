using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Account.Queries;

public class GetMyPayoutDetailsQueryHandler(IApplicationReadDbConnection dbConnection, IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetMyPayoutDetailsQuery, MyPayoutDetailsDto>
{
    public async Task<MyPayoutDetailsDto> Handle(GetMyPayoutDetailsQuery request, CancellationToken cancellationToken)
    {
        const string detailsSql = @"
            SELECT
                [AccountName] AS EncryptedAccountName,
                [SortCode] AS EncryptedSortCode,
                [AccountNumber] AS EncryptedAccountNumber
            FROM
                [UserPayoutDetails]
            WHERE
                [UserId] = @UserId;";

        var row = await dbConnection.QuerySingleOrDefaultAsync<PayoutDetailsRow>(
            detailsSql,
            cancellationToken,
            new { request.UserId });

        const string adminsSql = @"
            SELECT DISTINCT
                u.[FirstName] + ' ' + u.[LastName] AS AdminName
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
                AND l.[AdministratorUserId] <> @UserId
            ORDER BY
                u.[FirstName] + ' ' + u.[LastName];";

        var admins = (await dbConnection.QueryAsync<string>(
            adminsSql,
            cancellationToken,
            new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();

        var accountName = fieldEncryptionService.Decrypt(row?.EncryptedAccountName);
        var sortCode = fieldEncryptionService.Decrypt(row?.EncryptedSortCode);
        var accountNumber = fieldEncryptionService.Decrypt(row?.EncryptedAccountNumber);

        var hasDetails = accountName is not null && sortCode is not null && accountNumber is not null;

        return new MyPayoutDetailsDto(accountName, sortCode, accountNumber, hasDetails, admins);
    }

    private sealed record PayoutDetailsRow(
        string? EncryptedAccountName,
        string? EncryptedSortCode,
        string? EncryptedAccountNumber);
}
