using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IWinningsQuery"/> implementation must return.
///
/// The nullable fields carry the weight here. The entry deadline decides whether the page shows anything at all; the round
/// number and month decide which list a win belongs in, and each is set only for the kind of prize that has one.
/// </summary>
public abstract class WinningsQueryConformanceTests
{
    protected abstract IWinningsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId + 5_000, CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeagueAndSeasonFacts()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var header = data!.Header;
        header.TotalRoundsInSeason.Should().Be(38);
        header.SeasonStartDateUtc.Should().NotBe(default);
        header.SeasonEndDateUtc.Should().NotBe(default);
        header.EntryCost.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange - the column allows null, and this is the field the page compares against the clock.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Header.EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyApprovedMembersAsEntries()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the entry count multiplies into the prize pot, so a request must not inflate it.
        data!.Header.EntryCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheAdministratorsTopUp()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - returned even though this page's pot leaves it out, so the difference from every other page is visible
        // in the handler rather than hidden in a SELECT that omitted the column.
        data!.Header.PrizeFundOverride.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachPrizeSettingWithItsIdAndWording()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(
            world.LeagueId, PrizeType.Overall, 100m, prizeDescription: "1st Place");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the id is what ties a win back to the prize it came from.
        var setting = data!.PrizeSettings.Single();
        setting.Id.Should().Be(settingId);
        setting.PrizeType.Should().Be(PrizeType.Overall);
        setting.Name.Should().Be("1st Place");
        setting.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWording_ForAPrizeTheAdministratorDidNotName()
    {
        // Arrange - PrizeDescription allows null, and the old result type declared it non-nullable.
        var world = await ArrangeAsync();
        await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.PrizeSettings.Single().Name.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnARoundWinWithItsRoundNumberAndNoMonth()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);
        await Seed.AddWinningAsync(world.UserId, settingId, 5m, roundNumber: 7);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - which list a win belongs in depends on this, and a round prize has no month.
        var winning = data!.Winnings.Single();
        winning.RoundNumber.Should().Be(7);
        winning.Month.Should().BeNull();
        winning.PrizeType.Should().Be(PrizeType.Round);
        winning.LeaguePrizeSettingId.Should().Be(settingId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAMonthlyWinWithItsMonthAndNoRound()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Monthly, 20m);
        await Seed.AddWinningAsync(world.UserId, settingId, 20m, month: 3);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var winning = data!.Winnings.Single();
        winning.Month.Should().Be(3);
        winning.RoundNumber.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheWinnersNameParts()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m);
        await Seed.AddWinningAsync(world.UserId, settingId, 100m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - parts, because this page abbreviates the name and the payouts screen does not.
        var winning = data!.Winnings.Single();
        winning.FirstName.Should().Be("Ada");
        winning.LastName.Should().Be("Lovelace");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWinnings_FromAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var settingId = await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 500m);
        await Seed.AddWinningAsync(world.UserId, settingId, 500m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Winnings.Should().BeEmpty();
        data.PrizeSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the winnings table is the league, and a request to join is not yet part of it.
        data!.Members.Select(member => (member.FirstName, member.LastName))
            .Should().BeEquivalentTo([("Ada", "Lovelace")]);
    }

    private async Task<WinningsWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new WinningsWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record WinningsWorld(int LeagueId, int SeasonId, string UserId);
}
