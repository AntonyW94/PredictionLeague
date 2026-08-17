using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Persistence.SqlServer.Identity;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Schema;

/// <summary>
/// Admin "Delete user" ends in <c>DapperUserStore.DeleteAsync</c>, which is one statement:
///
/// <code>
/// DELETE FROM [AspNetUsers] WHERE [Id] = @Id;
/// </code>
///
/// Whether that succeeds is decided entirely by the schema, so nothing above it can be tested for this.
/// The handler's unit tests mock <c>IUserManager</c> and pass; the real delete threw error 547 against a
/// production account holding a single season pass, and because nothing catches <c>SqlException</c> the
/// admin saw <c>ErrorHandlingMiddleware</c>'s unhandled bucket - "An internal server error has occurred."
/// - with no indication of what was in the way.
///
/// These tests run the production store against the real schema built by the committed migrations, which
/// is the only place the answer exists. They are written to fail on the pre-0009 schema.
/// </summary>
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class UserDeletionCascadeTests(SqlServerDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheAccount_WhenTheUserHoldsASeasonPass()
    {
        // Arrange - the exact production case: one season pass, no leagues created or joined.
        var backdrop = await Seed.AddBackdropAsync();
        var userId = await Seed.AddUserAsync("Ada", "Lovelace");
        await Seed.AddSeasonPassAsync(userId, backdrop.SeasonId);

        // Act
        await DeleteUserAsync(userId);

        // Assert
        (await UserExistsAsync(userId)).Should().BeFalse(
            "a season pass is the user's own purchase record, and it blocked the delete with a 500.");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEveryDependentRecord_WhenTheUserHasOneOfEach()
    {
        // Arrange - a row in all nine tables 0009 cascades, so a constraint left behind fails here rather
        // than the next time an admin meets an account nobody thought about.
        var world = await ArrangeUserWithOneOfEverythingAsync();

        // Act
        await DeleteUserAsync(world.UserId);

        // Assert
        foreach (var (table, column) in CascadingTables)
        {
            var remaining = await ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [{table}] t WHERE t.[{column}] = @UserId;",
                new { world.UserId });

            remaining.Should().Be(0, $"[{table}] holds the deleted user's own records.");
        }
    }

    [Fact]
    public async Task DeleteAsync_ShouldLeaveOtherMembersRecordsAlone_WhenOneMemberIsDeleted()
    {
        // Arrange - the cascade is keyed on UserId, but every one of these tables is also keyed on a
        // league or a round shared with other players. A constraint written against the wrong column
        // would take their rows too, and the count assertions above would still pass.
        var world = await ArrangeUserWithOneOfEverythingAsync();

        // Act
        await DeleteUserAsync(world.UserId);

        // Assert
        foreach (var (table, column) in CascadingTables)
        {
            var survivors = await ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [{table}] t WHERE t.[{column}] = @UserId;",
                new { UserId = world.BystanderUserId });

            survivors.Should().Be(1, $"the bystander's [{table}] row belongs to them, not to the deleted account.");
        }
    }

    [Fact]
    public async Task DeleteAsync_ShouldLeaveTheLeagueStanding_WhenTheDeletedUserAdministeredIt()
    {
        // Arrange - [Leagues].[AdministratorUserId] is deliberately NOT cascaded. Cascading it would let
        // closing one account delete a league and every other member's history with it, so the delete is
        // still refused here and DeleteUserCommandHandler's "choose a new administrator" flow is what
        // resolves it. This test is the reason that exclusion cannot be quietly tidied away later.
        var backdrop = await Seed.AddBackdropAsync();
        var adminUserId = await Seed.AddUserAsync("Grace", "Hopper");
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, adminUserId);

        // Act
        var deleting = async () => await DeleteUserAsync(adminUserId);

        // Assert
        await deleting.Should().ThrowAsync<SqlException>(
            "the league outlives its administrator's account, so the reassignment has to happen first.");

        (await ScalarAsync<int>("SELECT COUNT(*) FROM [Leagues] l WHERE l.[Id] = @LeagueId;", new { LeagueId = leagueId }))
            .Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_ShouldLeaveThePrizeSchemeStanding_WhenItsAuthorIsDeleted()
    {
        // Arrange - [LeaguePrizeScheme].[SetByUserId] records who configured a league's prizes. The scheme
        // belongs to the league, so an unrelated account closing must not strip it. Also NOT cascaded.
        var backdrop = await Seed.AddBackdropAsync();
        var ownerUserId = await Seed.AddUserAsync("Katherine", "Johnson");
        var authorUserId = await Seed.AddUserAsync("Dorothy", "Vaughan");
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, ownerUserId, hasPrizes: true);
        var schemeId = await Seed.AddLeaguePrizeSchemeAsync(leagueId, authorUserId);

        // Act
        var deleting = async () => await DeleteUserAsync(authorUserId);

        // Assert
        await deleting.Should().ThrowAsync<SqlException>();

        (await ScalarAsync<int>("SELECT COUNT(*) FROM [LeaguePrizeScheme] s WHERE s.[Id] = @SchemeId;", new { SchemeId = schemeId }))
            .Should().Be(1, "a league keeps its prize configuration when the admin who wrote it leaves.");
    }

    #region Arrangement

    /// <summary>
    /// Every table <c>0009_CascadeUserDeletion.sql</c> switches to ON DELETE CASCADE, and the column
    /// carrying the user. Listed here rather than inside one test so both the "goes" and the "stays"
    /// assertions walk the same set.
    /// </summary>
    private static readonly (string Table, string Column)[] CascadingTables =
    [
        ("SeasonPasses", "UserId"),
        ("UserOnboardingSkips", "UserId"),
        ("Winnings", "UserId"),
        ("LeaguePayouts", "UserId"),
        ("LeagueMemberStats", "UserId"),
        ("LeagueRoundResults", "UserId"),
        ("RoundResults", "UserId"),
        ("LeagueWelcomeNotifications", "UserId"),
        ("PrizeNotifications", "UserId")
    ];

    private async Task<DeletionWorld> ArrangeUserWithOneOfEverythingAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();

        // The league belongs to a third party, so the account under test is an ordinary member - the
        // administrator case is its own test above.
        var ownerUserId = await Seed.AddUserAsync("Katherine", "Johnson");
        var userId = await Seed.AddUserAsync("Ada", "Lovelace");
        var bystanderUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, ownerUserId, hasPrizes: true);
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(-7));
        var prizeSettingId = await Seed.AddLeaguePrizeSettingAsync(leagueId, PrizeType.Round, prizeAmount: 10m);

        foreach (var member in new[] { userId, bystanderUserId })
        {
            await Seed.AddLeagueMemberAsync(leagueId, member);
            await Seed.AddSeasonPassAsync(member, backdrop.SeasonId);
            await Seed.AddWinningAsync(member, prizeSettingId, amount: 10m);
            await Seed.AddPrizeNotificationAsync(member, prizeSettingId);
            await Seed.AddLeaguePayoutAsync(leagueId, member, totalAmount: 10m, paidAtUtc: null);
            await Seed.AddLeagueMemberStatsAsync(leagueId, member, overallRank: 1);
            await Seed.AddLeagueRoundResultAsync(leagueId, roundId, member, basePoints: 9, boostedPoints: 9, appliedBoostCode: "NONE");
            await Seed.AddRoundResultAsync(roundId, member, exactScoreCount: 1);
            await Seed.AddLeagueWelcomeNotificationAsync(leagueId, member);

            // The seeder has no onboarding-skip method - the table is written only by
            // OnboardingSkipRepository and nothing reads it in a query test. A direct insert is the same
            // category as the rest of the arrangement: it bypasses the code under test on purpose.
            await ExecuteAsync(
                """
                INSERT INTO [UserOnboardingSkips]
                (
                    [UserId],
                    [StepKey],
                    [SkippedAtUtc]
                )
                VALUES
                (
                    @UserId,
                    @StepKey,
                    SYSUTCDATETIME()
                );
                """,
                new { UserId = member, StepKey = "welcome" });
        }

        return new DeletionWorld(userId, bystanderUserId);
    }

    /// <summary>
    /// The production store, not a hand-written DELETE - the point is that the statement the application
    /// actually issues now succeeds.
    /// </summary>
    private async Task DeleteUserAsync(string userId)
    {
        var store = new DapperUserStore(Substitute.For<IConfiguration>(), ConnectionFactory);

        await store.DeleteAsync(new ApplicationUser { Id = userId }, CancellationToken.None);
    }

    private async Task<bool> UserExistsAsync(string userId) =>
        await ScalarAsync<int>("SELECT COUNT(*) FROM [AspNetUsers] u WHERE u.[Id] = @UserId;", new { UserId = userId }) > 0;

    private sealed record DeletionWorld(string UserId, string BystanderUserId);

    #endregion
}
