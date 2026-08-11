using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueDetailQuery"/> implementation must return.
///
/// The nullable columns are the point. A league with no entry code and no deadline must report both as null, because
/// the words "Public" and the date in 1900 that a member actually sees are presentation decisions - and an adapter that
/// substituted them would make the handler unable to tell "not set" from "set to that".
/// </summary>
public abstract class LeagueDetailQueryConformanceTests
{
    protected abstract ILeagueDetailQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId + 5_000, CancellationToken.None);

        // Assert
        league.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesSettingsAndSeason()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        league!.Id.Should().Be(world.LeagueId);
        league.Name.Should().Be("Integration League");
        league.SeasonName.Should().Be("2026/27");
        league.SeasonId.Should().Be(world.SeasonId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoEntryCode_ForAPublicLeague()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - null, not the word "Public". That word is what the handler shows for a null.
        league!.EntryCode.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - null, not a date in 1900. The sentinel is the handler's, and an adapter inventing it would leave the
        // handler unable to tell an unset deadline from a real one.
        league!.EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountMembershipsBothWays()
    {
        // Arrange - two approved, one pending, one rejected.
        var world = await ArrangeAsync();

        var approvedId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, approvedId);

        var pendingId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        var rejectedId = await Seed.AddUserAsync("Edsger", "Dijkstra");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rejectedId, LeagueMemberStatus.Rejected);

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - both counts, so the difference between them is visible rather than a matter of which one the SQL
        // happened to compute. Which of the two is shown is the handler's rule.
        league!.TotalMembershipCount.Should().Be(4);
        league.ApprovedMemberCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountMembersOfAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var otherMemberId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(otherLeagueId, otherMemberId);

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        league!.TotalMembershipCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheCompetitionType()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the type itself, not a "is it a tournament" flag. Turning one into the other is the handler's rule.
        league!.CompetitionType.Should().BeOneOf(CompetitionType.League, CompetitionType.Tournament);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportNoPrizeScheme_WhenTheLeagueHasNotBeenGivenOne()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        league!.HasPrizeScheme.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnThePointsPerOutcome()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var league = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        league!.PointsForExactScore.Should().BeGreaterThan(0);
        league.PointsForCorrectResult.Should().BeGreaterThan(0);
    }

    private async Task<DetailWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new DetailWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record DetailWorld(int LeagueId, int SeasonId, string UserId);
}
