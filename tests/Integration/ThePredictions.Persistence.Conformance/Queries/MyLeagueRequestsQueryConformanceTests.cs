using FluentAssertions;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IMyLeagueRequestsQuery"/> implementation must return.
///
/// A dismissed rejection has to come back. Whether it is still worth showing is the handler's rule, and an adapter that
/// filtered on the dismissal flag would take that decision away from it.
/// </summary>
public abstract class MyLeagueRequestsQueryConformanceTests
{
    protected abstract IMyLeagueRequestsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThePlayerHasMadeNoRequests()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var requests = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPendingRequest()
    {
        // Arrange
        var world = await ArrangeAsync(LeagueMemberStatus.Pending);

        // Act
        var requests = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        var request = requests.Single();
        request.LeagueId.Should().Be(world.LeagueId);
        request.Status.Should().Be(LeagueMemberStatus.Pending);
        request.JoinedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnARejectedRequest()
    {
        // Arrange
        var world = await ArrangeAsync(LeagueMemberStatus.Rejected);

        // Act
        var requests = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - whether the player has dismissed the notice is the handler's business, so both cases arrive.
        requests.Single().Status.Should().Be(LeagueMemberStatus.Rejected);
        requests.Single().IsAlertDismissed.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnApprovedMembership()
    {
        // Arrange
        var world = await ArrangeAsync(LeagueMemberStatus.Approved);

        // Act
        var requests = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - an approved membership is not a request; it belongs on the My Leagues tile instead.
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnSomebodyElsesRequest()
    {
        // Arrange
        var world = await ArrangeAsync(LeagueMemberStatus.Pending);
        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, strangerId, LeagueMemberStatus.Pending);

        // Act
        var requests = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeagueSeasonAndAdministratorsNameParts()
    {
        // Arrange
        var world = await ArrangeAsync(LeagueMemberStatus.Pending);

        // Act
        var request = (await Query.ExecuteAsync(world.UserId, CancellationToken.None)).Single();

        // Assert - the administrator is the person the player is waiting on, so the tile names them.
        request.LeagueName.Should().Be("Integration League");
        request.SeasonName.Should().Be("2026/27");
        request.AdminFirstName.Should().Be("Grace");
        request.AdminLastName.Should().Be("Hopper");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheEntryDeadlineAsStored()
    {
        // Arrange - the seeded league has none, and this read has no deadline filter to hide that.
        var world = await ArrangeAsync(LeagueMemberStatus.Pending);

        // Act
        var request = (await Query.ExecuteAsync(world.UserId, CancellationToken.None)).Single();

        // Assert
        request.EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyApprovedMembers()
    {
        // Arrange - the administrator is approved, the player is waiting, a third person is also waiting.
        var world = await ArrangeAsync(LeagueMemberStatus.Pending);
        var otherWaiterId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, otherWaiterId, LeagueMemberStatus.Pending);

        // Act
        var request = (await Query.ExecuteAsync(world.UserId, CancellationToken.None)).Single();

        // Assert - the count feeds the pot the player is being shown, so requests must not inflate it.
        request.MemberCount.Should().Be(1);
    }

    private async Task<RequestsWorld> ArrangeAsync(LeagueMemberStatus status)
    {
        // The backdrop's user is the player making the request; somebody else runs the league.
        var backdrop = await Seed.AddBackdropAsync();

        var administratorId = await Seed.AddUserAsync("Grace", "Hopper");
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, administratorId);
        await Seed.AddLeagueMemberAsync(leagueId, administratorId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId, status);

        return new RequestsWorld(leagueId, backdrop.UserId);
    }

    private sealed record RequestsWorld(int LeagueId, string UserId);
}
