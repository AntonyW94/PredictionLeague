using FluentAssertions;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueMembershipQuery"/> implementation must return.
///
/// The guard in front of eighteen league queries, so the cases that must answer <c>false</c> matter more here than
/// the one that answers <c>true</c>: a pending request, a rejected one, another league's membership, and being a
/// member of a league you do not run.
/// </summary>
public abstract class LeagueMembershipQueryConformanceTests
{
    protected abstract ILeagueMembershipQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task IsApprovedMemberAsync_ShouldBeTrue_ForAnApprovedMember()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var isMember = await Query.IsApprovedMemberAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task IsApprovedMemberAsync_ShouldBeFalse_ForAPendingRequest()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var isMember = await Query.IsApprovedMemberAsync(world.LeagueId, pendingUserId, CancellationToken.None);

        // Assert - asking to join is not joining.
        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsApprovedMemberAsync_ShouldBeFalse_ForARejectedRequest()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rejectedUserId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rejectedUserId, LeagueMemberStatus.Rejected);

        // Act
        var isMember = await Query.IsApprovedMemberAsync(world.LeagueId, rejectedUserId, CancellationToken.None);

        // Assert
        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsApprovedMemberAsync_ShouldBeFalse_ForSomeoneWithNoMembershipAtAll()
    {
        // Arrange
        var world = await ArrangeAsync();
        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");

        // Act
        var isMember = await Query.IsApprovedMemberAsync(world.LeagueId, strangerId, CancellationToken.None);

        // Assert
        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsApprovedMemberAsync_ShouldBeFalse_ForMembershipOfADifferentLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");

        // Act - approved in the first league, nothing in the second.
        var isMember = await Query.IsApprovedMemberAsync(otherLeagueId, world.UserId, CancellationToken.None);

        // Assert
        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdministratorAsync_ShouldBeTrue_ForTheLeaguesAdministrator()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var isAdmin = await Query.IsAdministratorAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        isAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task IsAdministratorAsync_ShouldBeFalse_ForAnOrdinaryMember()
    {
        // Arrange
        var world = await ArrangeAsync();
        var memberId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, memberId);

        // Act
        var isAdmin = await Query.IsAdministratorAsync(world.LeagueId, memberId, CancellationToken.None);

        // Assert - being in the league is not the same as running it.
        isAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdministratorAsync_ShouldBeFalse_ForTheAdministratorOfAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherAdminId = await Seed.AddUserAsync("Alan", "Turing");
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, otherAdminId, "Their League");

        // Act
        var isAdmin = await Query.IsAdministratorAsync(world.LeagueId, otherAdminId, CancellationToken.None);

        // Assert
        isAdmin.Should().BeFalse();
        (await Query.IsAdministratorAsync(otherLeagueId, otherAdminId, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task IsAdministratorAsync_ShouldBeFalse_ForALeagueThatDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var isAdmin = await Query.IsAdministratorAsync(world.LeagueId + 5_000, world.UserId, CancellationToken.None);

        // Assert
        isAdmin.Should().BeFalse();
    }

    private async Task<MembershipWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new MembershipWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record MembershipWorld(int LeagueId, int SeasonId, string UserId);
}
