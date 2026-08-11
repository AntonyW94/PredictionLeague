using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueRoundsQuery"/> implementation must return.
///
/// Two callers share this read and keep different rounds from it - the dashboard lists all of them, the round picker
/// only those a player may look at. So an adapter that filtered by status would settle one caller's rule on the other's
/// behalf, and an adapter that ordered them would settle both.
/// </summary>
public abstract class LeagueRoundsQueryConformanceTests
{
    protected abstract ILeagueRoundsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForALeagueThatDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId + 5_000, CancellationToken.None);

        // Assert
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeason_WhateverItsStatus()
    {
        // Arrange
        var world = await ArrangeAsync();
        var draftId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(7), RoundStatus.Draft);
        var liveId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);
        var doneId = await Seed.AddRoundAsync(world.SeasonId, 3, DateTime.UtcNow.AddDays(-8), RoundStatus.Completed);

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - both callers filter for themselves, so all four statuses have to arrive.
        rounds.Select(round => round.RoundId).Should().BeEquivalentTo([draftId, liveId, doneId]);
        rounds.Single(round => round.RoundId == liveId).Status.Should().Be(RoundStatus.InProgress);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsDetails()
    {
        // Arrange
        var world = await ArrangeAsync();
        var startDateUtc = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
        var deadlineUtc = new DateTime(2026, 3, 6, 11, 30, 0, DateTimeKind.Utc);
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 6, deadlineUtc, RoundStatus.Published, startDateUtc);

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var round = rounds.Single();
        round.RoundId.Should().Be(roundId);
        round.SeasonId.Should().Be(world.SeasonId);
        round.RoundNumber.Should().Be(6);
        round.StartDateUtc.Should().BeCloseTo(startDateUtc, TimeSpan.FromSeconds(1));
        round.DeadlineUtc.Should().BeCloseTo(deadlineUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountEachRoundsFixtures()
    {
        // Arrange
        var world = await ArrangeAsync();
        var withFixtures = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddMatchAsync(withFixtures, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddMatchAsync(withFixtures, world.HomeTeamId, world.AwayTeamId);
        var empty = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(7));

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        rounds.Single(round => round.RoundId == withFixtures).MatchCount.Should().Be(2);
        rounds.Single(round => round.RoundId == empty).MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoRounds_FromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2027/28");
        await Seed.AddRoundAsync(otherSeasonId, 1, DateTime.UtcNow.AddDays(-1));

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        rounds.Should().BeEmpty();
    }

    private async Task<RoundsWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new RoundsWorld(
            leagueId, backdrop.CompetitionId, backdrop.SeasonId, backdrop.HomeTeamId, backdrop.AwayTeamId);
    }

    private sealed record RoundsWorld(int LeagueId, int CompetitionId, int SeasonId, int HomeTeamId, int AwayTeamId);
}
