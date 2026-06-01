using Dapper;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

public class PersonalDataVerifier(SqlConnection connection)
{
    public async Task VerifyAsync()
    {
        var failures = new List<string>();

        await VerifyNoRealEmailsAsync(failures);
        await VerifyPasswordHashesAsync(failures);
        await VerifyRefreshTokensEmptyAsync(failures);
        await VerifyPasswordResetTokensEmptyAsync(failures);
        await VerifyUserLoginsAsync(failures);
        await VerifyUserTokensEmptyAsync(failures);
        await VerifySeasonPassReferencesClearedAsync(failures);
        await VerifyLeagueBankDetailsClearedAsync(failures);

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.WriteLine($"[ERROR] Verification failed: {failure}");
            }

            throw new InvalidOperationException($"Personal data verification failed with {failures.Count} issue(s). See errors above.");
        }

        Console.WriteLine("[INFO] Personal data verification passed - no real personal data found");
    }

    private async Task VerifyNoRealEmailsAsync(List<string> failures)
    {
        var realEmails = await connection.QueryAsync<string>(
            """
            SELECT
                u.[Email]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Email] NOT LIKE '%@testmail.com'
                AND u.[Email] NOT LIKE '%@dev.local'
                AND u.[Email] NOT IN @PreservedEmails
            """,
            new { DataAnonymiser.PreservedEmails });

        var emailList = realEmails.ToList();
        if (emailList.Count > 0)
            failures.Add($"Found {emailList.Count} non-anonymised email(s): {string.Join(", ", emailList.Take(5))}");
    }

    private async Task VerifyPasswordHashesAsync(List<string> failures)
    {
        var invalidHashes = await connection.QueryFirstOrDefaultAsync<int>(
            """
            SELECT
                COUNT(*)
            FROM
                [AspNetUsers] u
            WHERE
                u.[PasswordHash] <> 'INVALIDATED'
                AND u.[Email] NOT LIKE '%@dev.local'
                AND u.[Email] NOT IN @PreservedEmails
            """,
            new { DataAnonymiser.PreservedEmails });

        if (invalidHashes > 0)
            failures.Add($"Found {invalidHashes} user(s) with non-invalidated password hashes");
    }

    private async Task VerifyRefreshTokensEmptyAsync(List<string> failures)
    {
        var count = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM [RefreshTokens]");
        if (count > 0)
            failures.Add($"RefreshTokens table is not empty ({count} rows)");
    }

    private async Task VerifyPasswordResetTokensEmptyAsync(List<string> failures)
    {
        var count = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM [PasswordResetTokens]");
        if (count > 0)
            failures.Add($"PasswordResetTokens table is not empty ({count} rows)");
    }

    private async Task VerifyUserLoginsAsync(List<string> failures)
    {
        var preservedUserIds = (await connection.QueryAsync<string>(
            """
            SELECT
                u.[Id]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Email] IN @Emails
            """,
            new { Emails = DataAnonymiser.PreservedEmails })).ToList();

        if (preservedUserIds.Count == 0)
            preservedUserIds.Add(string.Empty);

        var unauthorisedLogins = await connection.QueryFirstOrDefaultAsync<int>(
            """
            SELECT
                COUNT(*)
            FROM
                [AspNetUserLogins] l
            WHERE
                l.[UserId] NOT IN @PreservedUserIds
            """,
            new { PreservedUserIds = preservedUserIds });

        if (unauthorisedLogins > 0)
            failures.Add($"AspNetUserLogins contains {unauthorisedLogins} row(s) not belonging to preserved accounts");
    }

    private async Task VerifyUserTokensEmptyAsync(List<string> failures)
    {
        var count = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM [AspNetUserTokens]");
        if (count > 0)
            failures.Add($"AspNetUserTokens table is not empty ({count} rows)");
    }

    private async Task VerifySeasonPassReferencesClearedAsync(List<string> failures)
    {
        var count = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM [SeasonPasses] WHERE [StripePaymentReference] IS NOT NULL");
        if (count > 0)
            failures.Add($"SeasonPasses contains {count} row(s) with a non-null StripePaymentReference");
    }

    private async Task VerifyLeagueBankDetailsClearedAsync(List<string> failures)
    {
        var count = await connection.QueryFirstOrDefaultAsync<int>(
            @"SELECT COUNT(*) FROM [Leagues]
              WHERE [BankAccountName] IS NOT NULL
                 OR [BankSortCode] IS NOT NULL
                 OR [BankAccountNumber] IS NOT NULL");
        if (count > 0)
            failures.Add($"Leagues contains {count} row(s) with non-null bank details");
    }
}
