using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IMonthlyLeaderboardQuery"/> implementation must return.
///
/// The month scoping is the part worth pinning here, because it is the one thing this port does that the overall
/// leaderboard's does not: points and round statuses must both be limited to the requested calendar month, and
/// the member list must not be. Everything about totals, positions, names and ordering is the handler's and is
/// deliberately not asserted.
/// </summary>
public abstract class MonthlyLeaderboardQueryConformanceTests
{
    protected abstract IMonthlyLeaderboardQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyThatMonthsPoints()
    {
        // Arrange - one round in August, one in September, both scored.
        var world = await ArrangeAsync();
        var augustRound = await Seed.AddRoundAsync(world.SeasonId, 1, new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));
        var septemberRound = await Seed.AddRoundAsync(world.SeasonId, 2, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, augustRound, world.UserId, 9, 9, "NONE");
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, septemberRound, world.UserId, 50, 50, "NONE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, month: 8, CancellationToken.None);

        // Assert - September's 50 must not appear.
        data.RoundPoints.Select(p => p.BoostedPoints).Should().BeEquivalentTo([9]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyThatMonthsRoundStatuses()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc), RoundStatus.Completed);
        await Seed.AddRoundAsync(world.SeasonId, 2, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), RoundStatus.InProgress);
        await Seed.AddRoundAsync(world.SeasonId, 3, new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc), RoundStatus.Published);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, month: 8, CancellationToken.None);

        // Assert - the September round is not one of the month's.
        data.MonthRoundStatuses.Should().BeEquivalentTo([RoundStatus.Completed, RoundStatus.InProgress]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStillReturnEveryMember_WhenNoneScoredThatMonth()
    {
        // Arrange - a September round only, queried for August.
        var world = await ArrangeAsync();
        var septemberRound = await Seed.AddRoundAsync(world.SeasonId, 1, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, septemberRound, world.UserId, 50, 50, "NONE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, month: 8, CancellationToken.None);

        // Assert - the member list is not month-scoped; scoring them zero is the handler's rule.
        data.Members.Should().HaveCount(1);
        data.RoundPoints.Should().BeEmpty();
        data.MonthRoundStatuses.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers_WithTheirNameParts()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, month: 8, CancellationToken.None);

        // Assert
        data.Members.Select(m => (m.FirstName, m.LastName)).Should().BeEquivalentTo([("Ada", "Lovelace")]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportARoundInProgressAnywhereInTheSeason_NotOnlyInTheMonth()
    {
        // Arrange - the live round is in September; the flag drives a season-wide banner, so it is still true
        // when August is the month being asked for.
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, month: 8, CancellationToken.None);

        // Assert
        data.HasRoundInProgress.Should().BeTrue();
        data.MonthRoundStatuses.Should().BeEmpty("the live round is not one of August's.");
    }

    private async Task<MonthlyWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new MonthlyWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record MonthlyWorld(int LeagueId, int SeasonId, string UserId);
}
