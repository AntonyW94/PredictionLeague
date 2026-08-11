using FluentAssertions;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IJoinableLeaguesQuery"/> implementation must return.
///
/// Two obligations matter most. The entry code must be reported as a flag and never returned - these are leagues the player
/// is not a member of, so the code that would let them in is not theirs to have. And the deadline must come back as it is
/// stored, including null, because filtering on it in SQL is where the rule "a league with no deadline is never joinable"
/// used to hide.
/// </summary>
public abstract class JoinableLeaguesQueryConformanceTests
{
    protected abstract IJoinableLeaguesQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereAreNoLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var leagues = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueThePlayerIsNotIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Someone Else's League");
        await Seed.AddLeagueMemberAsync(leagueId, ownerId);

        // Act
        var leagues = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        leagues.Select(league => league.LeagueId).Should().Equal(leagueId);
    }

    [Theory]
    [InlineData(LeagueMemberStatus.Approved)]
    [InlineData(LeagueMemberStatus.Pending)]
    [InlineData(LeagueMemberStatus.Rejected)]
    public async Task ExecuteAsync_ShouldNotReturnALeagueThePlayerAlreadyHasAMembershipOf(LeagueMemberStatus status)
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId, status);

        // Act
        var leagues = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - any status. Somebody who was turned away is not offered the league a second time.
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportAnEntryCodeAsAFlagWithoutReturningIt()
    {
        // Arrange - the seeded league is public, so this pins the negative case; the row type has no field for the code at
        // all, which is what makes the positive case impossible to get wrong.
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Public League");

        // Act
        var leagues = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        leagues.Single().HasEntryCode.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheEntryDeadlineAsStored()
    {
        // Arrange - the seeded league has none, which is the case the old SQL filtered out invisibly.
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "No Deadline");

        // Act
        var leagues = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - null, not filtered away. Whether that makes the league joinable is LeagueEntry.IsOpen.
        leagues.Single().EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesNameSeasonAndStake()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Their League");

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Single();

        // Assert
        league.Name.Should().Be("Their League");
        league.SeasonName.Should().Be("2026/27");
        league.SeasonStartDateUtc.Should().NotBe(default);
        league.Price.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyApprovedMembers()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Their League");
        await Seed.AddLeagueMemberAsync(leagueId, ownerId);

        var pendingId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(leagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Single();

        // Assert - the count multiplies the stake into the estimated pot, so a request must not inflate it.
        league.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportNoSeasonPass_WhenThePlayerHasNotBoughtOne()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Their League");

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Single();

        // Assert - whether that hides the league is the handler's rule, and the two discovery queries answer it differently.
        league.HasSeasonPass.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportASeasonPass_WhenThePlayerHoldsOneForThatSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Their League");
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Single();

        // Assert
        league.HasSeasonPass.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReportAPassForADifferentSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var ownerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(backdrop.SeasonId, ownerId, "Their League");

        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");
        await Seed.AddSeasonPassAsync(backdrop.UserId, otherSeasonId);

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Single();

        // Assert
        league.HasSeasonPass.Should().BeFalse();
    }
}
