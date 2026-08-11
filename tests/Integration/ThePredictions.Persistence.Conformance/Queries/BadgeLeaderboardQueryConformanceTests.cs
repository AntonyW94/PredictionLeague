using FluentAssertions;
using ThePredictions.Application.Features.Badges.Queries;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IBadgeLeaderboardQuery"/> implementation must return: every account, and every award, unjoined.
///
/// Deliberately no filtering, no grouping and no ordering. Who counts as a player, what a badge total is and what
/// position each player holds were all decided in SQL before - twice over, since the dashboard tile had a second
/// statement that worked one player's position out differently.
/// </summary>
public abstract class BadgeLeaderboardQueryConformanceTests
{
    private static readonly DateTime AwardedUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    protected abstract IBadgeLeaderboardQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereAreNoAccounts()
    {
        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        data.Players.Should().BeEmpty();
        data.Awards.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBothPartsOfEachPlayersName()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - the displayed name abbreviates the surname and the full name settles joint positions, so neither
        // can be composed here.
        var player = data.Players.Single();
        player.FirstName.Should().Be("Ada");
        player.LastName.Should().Be("Lovelace");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAnAccountThatNeverFinishedSigningUp()
    {
        // Arrange - a sign-up with no name filled in.
        await Seed.AddBackdropAsync();
        await Seed.AddUserAsync(string.Empty, string.Empty);

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - whether such an account is a player is a rule, and it decides both who is listed and the "of 44
        // players" everyone is measured against. The read must not settle it.
        data.Players.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryAwardWithWhoEarnedIt()
    {
        // Arrange - two players, one of whom won the same badge twice.
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, AwardedUtc);
        var secondRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, AwardedUtc.AddDays(7));

        await Seed.AddUserBadgeAsync(backdrop.UserId, "round-winner", AwardedUtc, firstRoundId);
        await Seed.AddUserBadgeAsync(backdrop.UserId, "round-winner", AwardedUtc.AddDays(7), secondRoundId);
        await Seed.AddUserBadgeAsync(otherUserId, "founder", AwardedUtc);

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - three rows, not two players' totals: collapsing the repeat is the leaderboard's rule, and the
        // badges page counts the very same rows the other way.
        data.Awards.Should().HaveCount(3);
        data.Awards.Where(award => award.UserId == backdrop.UserId).Should().HaveCount(2);
        data.Awards.Single(award => award.UserId == otherUserId).BadgeKey.Should().Be("founder");
        data.Awards.Select(award => award.AwardedUtc).Should().Contain(AwardedUtc.AddDays(7));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPlayerWhoHasEarnedNothing()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - everybody is on the table, so an unawarded player arrives with no awards rather than not at all.
        data.Players.Should().HaveCount(1);
        data.Awards.Should().BeEmpty();
    }
}
