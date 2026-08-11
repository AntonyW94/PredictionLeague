using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeaguePrizesPageQuery"/> implementation must return.
///
/// The header and the prizes come back separately, so the case that used to be awkward is now plain: a league with no
/// prizes still has a header, and a league with four prizes has one header rather than four copies of it.
/// </summary>
public abstract class LeaguePrizesPageQueryConformanceTests
{
    protected abstract ILeaguePrizesPageQuery Query { get; }

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
    public async Task ExecuteAsync_ShouldReturnTheLeagueAndSeasonDetails()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var header = data!.Header;
        header.LeagueName.Should().Be("Integration League");
        header.NumberOfRounds.Should().Be(38);
        header.SeasonStartDateUtc.Should().NotBe(default);
        header.SeasonEndDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange - the column allows null, and what "none" looks like on screen is the handler's decision.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Header.EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountMembershipsBothWays()
    {
        // Arrange - two approved, one pending.
        var world = await ArrangeAsync();

        var approvedId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, approvedId);

        var pendingId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - both, so which one drives the pot preview stays the handler's decision and stays visible.
        data!.Header.TotalMembershipCount.Should().Be(3);
        data.Header.ApprovedMemberCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachPrizeAsConfigured()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m, rank: 1);
        await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Monthly, 25m, rank: 1);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the prize type has to arrive as the enum: the column holds its numeric value as text.
        data!.PrizeSettings.Should().HaveCount(2);
        data.PrizeSettings.Single(prize => prize.PrizeType == PrizeType.Overall).PrizeAmount.Should().Be(100m);
        data.PrizeSettings.Single(prize => prize.PrizeType == PrizeType.Monthly).Rank.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPrizes_ForALeagueThatHasNotSetAnyUp()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - an empty list, and a header that is still there. The old left join answered this with a row of nulls.
        data!.PrizeSettings.Should().BeEmpty();
        data.Header.LeagueName.Should().Be("Integration League");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPrizes_FromAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 500m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.PrizeSettings.Should().BeEmpty();
    }

    private async Task<PrizesWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new PrizesWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record PrizesWorld(int LeagueId, int SeasonId, string UserId);
}
