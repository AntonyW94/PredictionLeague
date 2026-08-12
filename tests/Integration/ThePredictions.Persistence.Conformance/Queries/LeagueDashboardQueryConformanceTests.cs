using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueDashboardQuery"/> implementation must return.
///
/// The members come back whatever their status, because which of them the dashboard lists is a rule - and the one it
/// leaves out is a person who was turned away, so an adapter filtering "helpfully" would settle a decision about
/// somebody's visibility on its own. The rounds are a separate port, shared with the dashboard's round picker.
/// </summary>
public abstract class LeagueDashboardQueryConformanceTests
{
    protected abstract ILeagueDashboardQuery Query { get; }

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
    public async Task ExecuteAsync_ShouldReturnTheLeagueAndItsSeason()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var header = data!.Header;
        header.Name.Should().Be("Integration League");
        // The rounds that exist, not the number the season declares - which is what "is the season over" is now decided
        // from everywhere. These worlds seed no rounds, so the count is nought rather than the season's 38.
        header.SeasonRoundCount.Should().Be(0);
        header.SeasonStartDateUtc.Should().NotBe(default);
        header.IsFree.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnThePotsIngredientsRatherThanThePot()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the entry fee, the head count and any top-up. Multiplying them is PrizeFund.Total.
        var header = data!.Header;
        header.MemberCount.Should().Be(2);
        header.Price.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyApprovedMembers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the count multiplies the entry fee into the pot, so a request must not inflate it.
        data!.Header.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheSeasonsCompletedRounds()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - whether that means the league is over is the handler's rule.
        data!.Header.CompletedRoundCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryMembership_WhateverItsStatus()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);
        var rejectedId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rejectedId, LeagueMemberStatus.Rejected);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - which of them the dashboard shows is the handler's rule, and it excludes the rejected one.
        data!.Members.Select(member => member.Status).Should().BeEquivalentTo(
        [
            LeagueMemberStatus.Approved,
            LeagueMemberStatus.Pending,
            LeagueMemberStatus.Rejected
        ]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMembersNamePartsAndJoinDate()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - parts rather than a formatted name: the dashboard abbreviates it but orders by both.
        var member = data!.Members.Single();
        member.FirstName.Should().Be("Ada");
        member.LastName.Should().Be("Lovelace");
        member.JoinedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoMembers_FromAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var otherMemberId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(otherLeagueId, otherMemberId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Members.Should().HaveCount(1);
    }

    private async Task<DashboardWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new DashboardWorld(
            leagueId, backdrop.CompetitionId, backdrop.SeasonId, backdrop.UserId,
            backdrop.HomeTeamId, backdrop.AwayTeamId);
    }

    private sealed record DashboardWorld(
        int LeagueId,
        int CompetitionId,
        int SeasonId,
        string UserId,
        int HomeTeamId,
        int AwayTeamId);
}
