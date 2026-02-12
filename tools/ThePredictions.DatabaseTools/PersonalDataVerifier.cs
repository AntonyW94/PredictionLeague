using Dapper;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

public class PersonalDataVerifier
{
    private readonly SqlConnection _connection;

    public PersonalDataVerifier(SqlConnection connection)
    {
        _connection = connection;
    }

    public async Task VerifyAsync()
    {
        var failures = new List<string>();

        await VerifyNoRealEmailsAsync(failures);
        await VerifyPasswordHashesAsync(failures);
        await VerifyRefreshTokensEmptyAsync(failures);
        await VerifyPasswordResetTokensEmptyAsync(failures);
        await VerifyUserLoginsAsync(failures);
        await VerifyUserTokensEmptyAsync(failures);

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.WriteLine($"[ERROR] Verification failed: {failure}");
            }

            throw new InvalidOperationException(
                $"Personal data verification failed with {failures.Count} issue(s). See errors above.");
        }

        Console.WriteLine("[INFO] Personal data verification passed - no real personal data found");
    }

    private async Task VerifyNoRealEmailsAsync(List<string> failures)
    {
        var realEmails = await _connection.QueryAsync<string>(
            """
            SELECT
                u.[Email]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Email] NOT LIKE '%@testmail.com'
                AND u.[Email] NOT LIKE '%@dev.local'
                AND u.[Email] <> @PreservedEmail
            """,
            new { PreservedEmail = DataAnonymiser.PreservedEmail });

        var emailList = realEmails.ToList();

        if (emailList.Count > 0)
        {
            failures.Add($"Found {emailList.Count} non-anonymised email(s): {string.Join(", ", emailList.Take(5))}");
        }
    }

    private async Task VerifyPasswordHashesAsync(List<string> failures)
    {
        var invalidHashes = await _connection.QueryFirstOrDefaultAsync<int>(
            """
            SELECT
                COUNT(*)
            FROM
                [AspNetUsers] u
            WHERE
                u.[PasswordHash] <> 'INVALIDATED'
                AND u.[Email] NOT LIKE '%@dev.local'
                AND u.[Email] <> @PreservedEmail
            """,
            new { PreservedEmail = DataAnonymiser.PreservedEmail });

        if (invalidHashes > 0)
        {
            failures.Add($"Found {invalidHashes} user(s) with non-invalidated password hashes");
        }
    }

    private async Task VerifyRefreshTokensEmptyAsync(List<string> failures)
    {
        var count = await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM [RefreshTokens]");

        if (count > 0)
        {
            failures.Add($"RefreshTokens table is not empty ({count} rows)");
        }
    }

    private async Task VerifyPasswordResetTokensEmptyAsync(List<string> failures)
    {
        var count = await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM [PasswordResetTokens]");

        if (count > 0)
        {
            failures.Add($"PasswordResetTokens table is not empty ({count} rows)");
        }
    }

    private async Task VerifyUserLoginsAsync(List<string> failures)
    {
        var preservedUserId = await _connection.QueryFirstOrDefaultAsync<string>(
            """
            SELECT
                u.[Id]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Email] = @Email
            """,
            new { Email = DataAnonymiser.PreservedEmail });

        var unauthorisedLogins = await _connection.QueryFirstOrDefaultAsync<int>(
            """
            SELECT
                COUNT(*)
            FROM
                [AspNetUserLogins] l
            WHERE
                l.[UserId] <> @PreservedUserId
            """,
            new { PreservedUserId = preservedUserId ?? "" });

        if (unauthorisedLogins > 0)
        {
            failures.Add($"AspNetUserLogins contains {unauthorisedLogins} row(s) not belonging to preserved account");
        }
    }

    private async Task VerifyUserTokensEmptyAsync(List<string> failures)
    {
        var count = await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM [AspNetUserTokens]");

        if (count > 0)
        {
            failures.Add($"AspNetUserTokens table is not empty ({count} rows)");
        }
    }
}
