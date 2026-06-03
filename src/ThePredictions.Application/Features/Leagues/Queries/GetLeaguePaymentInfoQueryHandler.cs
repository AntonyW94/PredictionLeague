using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeaguePaymentInfoQueryHandler(IApplicationReadDbConnection dbConnection, IFieldEncryptionService fieldEncryptionService)
    : IRequestHandler<GetLeaguePaymentInfoQuery, LeaguePaymentInfoDto>
{
    public async Task<LeaguePaymentInfoDto> Handle(GetLeaguePaymentInfoQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Name] AS LeagueName,
                l.[Price] AS Amount,
                l.[EntryCode] AS EntryCode,
                l.[BankAccountName] AS EncryptedAccountName,
                l.[BankSortCode] AS EncryptedSortCode,
                l.[BankAccountNumber] AS EncryptedAccountNumber,
                l.[PaymentReferenceTemplate] AS PaymentReferenceTemplate,
                CAST(CASE WHEN l.[AdministratorUserId] = @UserId THEN 1 ELSE 0 END AS BIT) AS IsAdmin,
                CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM [LeagueMembers] lm
                    WHERE lm.[LeagueId] = l.[Id]
                        AND lm.[UserId] = @UserId
                ) THEN 1 ELSE 0 END AS BIT) AS IsMember,
                u.[FirstName] AS RequestingFirstName,
                u.[LastName] AS RequestingLastName
            FROM
                [Leagues] l
            CROSS JOIN
                (SELECT [FirstName], [LastName] FROM [AspNetUsers] WHERE [Id] = @UserId) u
            WHERE
                l.[Id] = @LeagueId;";

        var row = await dbConnection.QuerySingleOrDefaultAsync<PaymentInfoRow>(
            sql,
            cancellationToken,
            new { request.LeagueId, UserId = request.RequestingUserId });

        if (row is null)
            throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");

        // A prospective joiner holding the matching entry code is authorised alongside admins/members.
        var hasValidEntryCode = !string.IsNullOrWhiteSpace(request.EntryCode)
            && string.Equals(row.EntryCode, request.EntryCode, StringComparison.OrdinalIgnoreCase);

        if (!row.IsAdmin && !row.IsMember && !hasValidEntryCode)
            throw new UnauthorizedAccessException("Only the league administrator or its members can view payment details.");

        var accountName = fieldEncryptionService.Decrypt(row.EncryptedAccountName);
        var sortCode = fieldEncryptionService.Decrypt(row.EncryptedSortCode);
        var accountNumber = fieldEncryptionService.Decrypt(row.EncryptedAccountNumber);

        var hasBankDetails = accountName is not null && sortCode is not null && accountNumber is not null;

        var reference = !string.IsNullOrWhiteSpace(row.PaymentReferenceTemplate)
            ? row.PaymentReferenceTemplate
            : $"{row.RequestingFirstName} {row.RequestingLastName}".Trim();

        return new LeaguePaymentInfoDto(
            row.LeagueName,
            hasBankDetails,
            accountName,
            sortCode,
            accountNumber,
            row.Amount,
            reference);
    }

    private sealed record PaymentInfoRow(
        string LeagueName,
        decimal Amount,
        string? EntryCode,
        string? EncryptedAccountName,
        string? EncryptedSortCode,
        string? EncryptedAccountNumber,
        string? PaymentReferenceTemplate,
        bool IsAdmin,
        bool IsMember,
        string? RequestingFirstName,
        string? RequestingLastName);
}
