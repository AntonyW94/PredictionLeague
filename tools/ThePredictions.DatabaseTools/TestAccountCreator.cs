using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

public class TestAccountCreator(SqlConnection connection, string testPassword)
{
    public async Task CreateTestAccountsAsync()
    {
        var hasher = new PasswordHasher<object>();
        var passwordHash = hasher.HashPassword(new object(), testPassword);

        var playerUserId = Guid.NewGuid().ToString();
        var adminUserId = Guid.NewGuid().ToString();

        await CreateUserAsync(playerUserId, "testplayer@dev.local", "TestPlayer", passwordHash);
        Console.WriteLine("[INFO] Created test player account: testplayer@dev.local");

        await CreateUserAsync(adminUserId, "testadmin@dev.local", "TestAdmin", passwordHash);
        Console.WriteLine("[INFO] Created test admin account: testadmin@dev.local");

        await AssignAdminRoleAsync(adminUserId);
        Console.WriteLine("[INFO] Assigned Admin role to testadmin@dev.local");

        await AddToFirstLeagueAsync(playerUserId);
        await AddToFirstLeagueAsync(adminUserId);
        Console.WriteLine("[INFO] Added both test accounts to the first league");
    }

    private async Task CreateUserAsync(string userId, string email, string firstName, string passwordHash)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO [AspNetUsers] (
                [Id],
                [UserName],
                [NormalizedUserName],
                [Email],
                [NormalizedEmail],
                [EmailConfirmed],
                [PasswordHash],
                [SecurityStamp],
                [ConcurrencyStamp],
                [PhoneNumber],
                [PhoneNumberConfirmed],
                [TwoFactorEnabled],
                [LockoutEnd],
                [LockoutEnabled],
                [AccessFailedCount],
                [FirstName],
                [LastName]
            )
            VALUES (
                @Id,
                @UserName,
                @NormalizedUserName,
                @Email,
                @NormalizedEmail,
                @EmailConfirmed,
                @PasswordHash,
                @SecurityStamp,
                @ConcurrencyStamp,
                @PhoneNumber,
                @PhoneNumberConfirmed,
                @TwoFactorEnabled,
                @LockoutEnd,
                @LockoutEnabled,
                @AccessFailedCount,
                @FirstName,
                @LastName
            )
            """,
            new
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                PasswordHash = passwordHash,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                PhoneNumber = (string?)null,
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnd = (DateTimeOffset?)null,
                LockoutEnabled = false,
                AccessFailedCount = 0,
                FirstName = firstName,
                LastName = "Tester"
            });
    }

    private async Task AssignAdminRoleAsync(string userId)
    {
        var adminRoleId = await connection.QueryFirstOrDefaultAsync<string>(
            """
            SELECT
                r.[Id]
            FROM
                [AspNetRoles] r
            WHERE
                r.[NormalizedName] = @NormalizedName
            """,
            new { NormalizedName = "ADMIN" });

        if (adminRoleId is null)
        {
            Console.WriteLine("[WARN] Admin role not found, skipping role assignment");
            return;
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO [AspNetUserRoles] (
                [UserId],
                [RoleId]
            )
            VALUES (
                @UserId,
                @RoleId
            )
            """,
            new { UserId = userId, RoleId = adminRoleId });
    }

    private async Task AddToFirstLeagueAsync(string userId)
    {
        var firstLeagueId = await connection.QueryFirstOrDefaultAsync<int?>(
            """
            SELECT TOP 1
                l.[Id]
            FROM
                [Leagues] l
            ORDER BY
                l.[Id]
            """);

        if (firstLeagueId is null)
        {
            Console.WriteLine("[WARN] No leagues found, skipping league membership");
            return;
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO [LeagueMembers] (
                [LeagueId],
                [UserId],
                [Status],
                [IsAlertDismissed],
                [JoinedAtUtc],
                [ApprovedAtUtc]
            )
            VALUES (
                @LeagueId,
                @UserId,
                @Status,
                @IsAlertDismissed,
                @JoinedAtUtc,
                @ApprovedAtUtc
            )
            """,
            new
            {
                LeagueId = firstLeagueId.Value,
                UserId = userId,
                Status = "Approved",
                IsAlertDismissed = false,
                JoinedAtUtc = DateTime.UtcNow,
                ApprovedAtUtc = DateTime.UtcNow
            });
    }
}
