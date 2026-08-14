using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

/// <summary>
/// Creates the accounts the dev site is driven by, after <see cref="DataAnonymiser"/> has invalidated every
/// copied password hash. These are the only accounts that can sign in on dev at all, so between them they
/// have to cover the states the E2E smoke suite asserts on:
///
/// <list type="bullet">
///   <item><c>testplayer@dev.local</c> - a settled player: Season Pass plus league membership, so the
///         dashboard renders its real tiles.</item>
///   <item><c>testadmin@dev.local</c> - the same, plus the Administrator role.</item>
///   <item><c>testnewplayer@dev.local</c> - a fresh sign-up: no pass and no league, which is what puts the
///         dashboard into its onboarding takeover.</item>
/// </list>
///
/// The Season Pass matters more than it looks. <c>get-pass</c> is a *required* onboarding step, so without a
/// pass row every account sits in the takeover and the dashboard never renders a league or a leaderboard -
/// which is exactly the page the smoke suite exists to watch.
/// </summary>
public class TestAccountCreator(SqlConnection connection, string testPassword)
{
    private const string PlayerEmail = "testplayer@dev.local";
    private const string AdminEmail = "testadmin@dev.local";
    private const string NewPlayerEmail = "testnewplayer@dev.local";

    public async Task CreateTestAccountsAsync()
    {
        var hasher = new PasswordHasher<object>();
        var passwordHash = hasher.HashPassword(new object(), testPassword);

        var playerUserId = Guid.NewGuid().ToString();
        var adminUserId = Guid.NewGuid().ToString();
        var newPlayerUserId = Guid.NewGuid().ToString();

        await CreateUserAsync(playerUserId, PlayerEmail, "TestPlayer", passwordHash);
        Console.WriteLine($"[INFO] Created test player account: {PlayerEmail}");

        await CreateUserAsync(adminUserId, AdminEmail, "TestAdmin", passwordHash);
        Console.WriteLine($"[INFO] Created test admin account: {AdminEmail}");

        await CreateUserAsync(newPlayerUserId, NewPlayerEmail, "TestNewPlayer", passwordHash);
        Console.WriteLine($"[INFO] Created test new-player account: {NewPlayerEmail} (deliberately no pass and no league)");

        await AssignAdminRoleAsync(adminUserId);
        Console.WriteLine($"[INFO] Assigned Admin role to {AdminEmail}");

        await GiveFirstLeagueAndSeasonPassAsync(playerUserId, adminUserId);
    }

    /// <summary>
    /// Puts the player and admin accounts into the first league and grants each a pass for that league's
    /// season. Both happen together on purpose: a league membership without a pass leaves the account stuck
    /// in the onboarding takeover, and a pass without a membership gives it nothing to look at.
    /// </summary>
    private async Task GiveFirstLeagueAndSeasonPassAsync(params string[] userIds)
    {
        var firstLeague = await GetFirstLeagueAsync();

        if (firstLeague is null)
        {
            Console.WriteLine("[WARN] No leagues found, skipping league membership and Season Passes");
            return;
        }

        foreach (var userId in userIds)
        {
            await AddToLeagueAsync(firstLeague.Id, userId);
            await GrantFreeSeasonPassAsync(firstLeague.SeasonId, userId);
        }

        Console.WriteLine(
            $"[INFO] Added the player and admin accounts to league {firstLeague.Id} "
            + $"and granted each a free Season Pass for season {firstLeague.SeasonId}");
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
            new { NormalizedName = "ADMINISTRATOR" });

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

    private async Task<FirstLeagueRow?> GetFirstLeagueAsync() =>
        await connection.QueryFirstOrDefaultAsync<FirstLeagueRow>(
            """
            SELECT TOP 1
                l.[Id],
                l.[SeasonId]
            FROM
                [Leagues] l
            ORDER BY
                l.[Id]
            """);

    private async Task AddToLeagueAsync(int leagueId, string userId)
    {
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
                LeagueId = leagueId,
                UserId = userId,
                Status = "Approved",
                IsAlertDismissed = false,
                JoinedAtUtc = DateTime.UtcNow,
                ApprovedAtUtc = DateTime.UtcNow
            });
    }

    /// <summary>
    /// Mirrors <c>SeasonPass.CreateFree</c>: Standard tier, nothing paid, and a null Stripe reference - which
    /// is also what keeps <see cref="PersonalDataVerifier"/> happy, since it fails the refresh on any pass row
    /// carrying a payment reference.
    /// </summary>
    private async Task GrantFreeSeasonPassAsync(int seasonId, string userId)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO [SeasonPasses] (
                [UserId],
                [SeasonId],
                [Tier],
                [Source],
                [AmountPaid],
                [SmsFeePaid],
                [StripePaymentReference],
                [CreatedAtUtc],
                [SmsSentCount],
                [RewardRedeemedForSeasonId]
            )
            VALUES (
                @UserId,
                @SeasonId,
                @Tier,
                @Source,
                @AmountPaid,
                @SmsFeePaid,
                @StripePaymentReference,
                @CreatedAtUtc,
                @SmsSentCount,
                @RewardRedeemedForSeasonId
            )
            """,
            new
            {
                UserId = userId,
                SeasonId = seasonId,
                Tier = "Standard",
                Source = "Free",
                AmountPaid = 0m,
                SmsFeePaid = 0m,
                StripePaymentReference = (string?)null,
                CreatedAtUtc = DateTime.UtcNow,
                SmsSentCount = 0,
                RewardRedeemedForSeasonId = (int?)null
            });
    }

    private record FirstLeagueRow(int Id, int SeasonId);
}
