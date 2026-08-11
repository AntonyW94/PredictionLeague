using FluentAssertions;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IAdminPendingMembersQuery"/> implementation must return.
///
/// Every league the player administers, open or closed. Which of them still count as taking entries turns on a clock, and
/// deciding it here would put that comparison back where no test can reach it.
/// </summary>
public abstract class AdminPendingMembersQueryConformanceTests
{
    protected abstract IAdminPendingMembersQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForSomebodyWhoRunsNoLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var administratorId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, administratorId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Leagues.Should().BeEmpty();
        data.PendingMembers.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueThePlayerRuns()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None);

        // Assert
        var league = data.Leagues.Single();
        league.LeagueId.Should().Be(world.LeagueId);
        league.LeagueName.Should().Be("Integration League");
        league.Price.Should().Be(0m);
        league.IsFree.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueWhateverItsDeadline()
    {
        // Arrange - the seeded league has no deadline at all, which the old statement's comparison filtered away.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None);

        // Assert - returned, with a null deadline. Whether it still counts as open is the handler's rule.
        data.Leagues.Single().EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueSomebodyElseRuns()
    {
        // Arrange
        var world = await ArrangeAsync();
        var strangerId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueAsync(world.SeasonId, strangerId, "Their League");

        // Act
        var data = await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None);

        // Assert
        data.Leagues.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountMembersAndRequestsSeparately()
    {
        // Arrange - two approved including the administrator, and two waiting.
        var world = await ArrangeAsync();

        var memberId = await Seed.AddUserAsync("Ada", "Lovelace");
        await Seed.AddLeagueMemberAsync(world.LeagueId, memberId);

        var firstWaiterId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, firstWaiterId, LeagueMemberStatus.Pending);

        var secondWaiterId = await Seed.AddUserAsync("Edsger", "Dijkstra");
        await Seed.AddLeagueMemberAsync(world.LeagueId, secondWaiterId, LeagueMemberStatus.Pending);

        // Act
        var league = (await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None)).Leagues.Single();

        // Assert
        league.MemberCount.Should().Be(2);
        league.PendingCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesEntryCode()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = (await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None)).Leagues.Single();

        // Assert - unlike the league-discovery rows, the code belongs to this reader: it is their league to share.
        league.EntryCode.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachWaitingPlayerWithTheirNameParts()
    {
        // Arrange
        var world = await ArrangeAsync();
        var waiterId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, waiterId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None);

        // Assert - the id is what the approve and reject buttons act on.
        var pending = data.PendingMembers.Single();
        pending.UserId.Should().Be(waiterId);
        pending.FirstName.Should().Be("Alan");
        pending.LastName.Should().Be("Turing");
        pending.LeagueId.Should().Be(world.LeagueId);
        pending.LeagueName.Should().Be("Integration League");
        pending.JoinedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnApprovedOrRejectedMemberAsWaiting()
    {
        // Arrange
        var world = await ArrangeAsync();

        var approvedId = await Seed.AddUserAsync("Ada", "Lovelace");
        await Seed.AddLeagueMemberAsync(world.LeagueId, approvedId);

        var rejectedId = await Seed.AddUserAsync("Edsger", "Dijkstra");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rejectedId, LeagueMemberStatus.Rejected);

        // Act
        var data = await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None);

        // Assert - this list is decisions still to make, and both of those have been made.
        data.PendingMembers.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnRequestsToALeagueSomebodyElseRuns()
    {
        // Arrange
        var world = await ArrangeAsync();
        var strangerId = await Seed.AddUserAsync("Alan", "Turing");
        var theirLeagueId = await Seed.AddLeagueAsync(world.SeasonId, strangerId, "Their League");

        var waiterId = await Seed.AddUserAsync("Edsger", "Dijkstra");
        await Seed.AddLeagueMemberAsync(theirLeagueId, waiterId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.AdministratorId, CancellationToken.None);

        // Assert
        data.PendingMembers.Should().BeEmpty();
    }

    private async Task<AdminWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new AdminWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record AdminWorld(int LeagueId, int SeasonId, string AdministratorId);
}
