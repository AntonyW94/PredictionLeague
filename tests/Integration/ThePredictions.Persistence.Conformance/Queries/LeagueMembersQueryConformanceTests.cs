using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueMembersQuery"/> implementation must return.
///
/// The case to pin is a league with nobody in it: the name still has to come back, and only a league that genuinely does
/// not exist may answer <c>null</c>. The old handler decided those two apart by whether the member list was empty.
/// </summary>
public abstract class LeagueMembersQueryConformanceTests
{
    protected abstract ILeagueMembersQuery Query { get; }

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
    public async Task ExecuteAsync_ShouldReturnTheLeaguesName_EvenWithNobodyInIt()
    {
        // Arrange - a league with no memberships at all.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Empty League");

        // Act
        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert - the page still has a heading to show.
        data.Should().NotBeNull();
        data!.LeagueName.Should().Be("Empty League");
        data.Members.Should().BeEmpty();
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

        // Assert - the administrator approves and rejects from this page, so all three have to arrive.
        data!.Members.Select(member => member.Status).Should().BeEquivalentTo(
        [
            LeagueMemberStatus.Approved,
            LeagueMemberStatus.Pending,
            LeagueMemberStatus.Rejected
        ]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachMembersIdNamePartsAndJoinDate()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the id is what the approve and reject buttons act on; the name parts are ordered and abbreviated by
        // the handler.
        var member = data!.Members.Single();
        member.UserId.Should().Be(world.UserId);
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

    private async Task<MembersWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new MembersWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record MembersWorld(int LeagueId, int SeasonId, string UserId);
}
