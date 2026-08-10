using FluentAssertions;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueBoostUsageQuery"/> implementation must return.
///
/// The important assertion is that usages come back <b>uncensored</b>. The secrecy rule moved to C# with the
/// persistence split, so an adapter that helpfully filtered here would break the rule rather than enforce it:
/// the handler would censor already-censored rows, and every player's remaining-uses figure would be computed
/// from an incomplete set. This suite pins the port's promise so a future adapter cannot make that mistake,
/// and the round deadline is returned precisely so the rule has something to compare against.
/// </summary>
public abstract class LeagueBoostUsageQueryConformanceTests
{
    protected abstract ILeagueBoostUsageQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        (await Query.ExecuteAsync(leagueId: 987654, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryUsageUncensored_WhateverTheRoundDeadlines()
    {
        // Arrange - two players, one open round and one closed.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - four usages, none filtered.
        data.Should().NotBeNull();
        data!.Usages.Select(u => (u.UserId, u.RoundNumber))
            .Should().BeEquivalentTo(
            [
                (world.MeUserId, world.OpenRoundNumber), (world.MeUserId, world.ClosedRoundNumber),
                (world.OpponentUserId, world.OpenRoundNumber), (world.OpponentUserId, world.ClosedRoundNumber)
            ],
            "the port promises raw facts - an adapter that filtered by deadline would break the visibility "
            + "rule rather than apply it, because the handler would then censor an already-incomplete set.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsDeadline_SoTheVisibilityRuleHasSomethingToCompare()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - to the second, because the arrangement is relative to now.
        data!.Usages.First(u => u.RoundNumber == world.OpenRoundNumber).RoundDeadlineUtc
            .Should().BeCloseTo(world.OpenRoundDeadlineUtc, TimeSpan.FromSeconds(1));

        data.Usages.First(u => u.RoundNumber == world.ClosedRoundNumber).RoundDeadlineUtc
            .Should().BeCloseTo(world.ClosedRoundDeadlineUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRawScoringFields_RatherThanAComputedPointsFigure()
    {
        // Arrange - only the closed round was scored.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the scored round carries base and boosted points for the C# rule to subtract.
        var scored = data!.Usages.Single(u => u.UserId == world.MeUserId && u.RoundNumber == world.ClosedRoundNumber);
        scored.HasBoost.Should().BeTrue();
        scored.BasePoints.Should().Be(9);
        scored.BoostedPoints.Should().Be(18);

        // And the unscored round carries nulls rather than zeroes, so "not scored yet" stays distinguishable
        // from "gained nothing".
        var unscored = data.Usages.Single(u => u.UserId == world.MeUserId && u.RoundNumber == world.OpenRoundNumber);
        unscored.HasBoost.Should().BeFalse();
        unscored.BasePoints.Should().BeNull();
        unscored.BoostedPoints.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNamePartsForEachApprovedMember_NotAFormattedName()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - formatting is PlayerDisplayName's job, so the port returns the parts.
        data!.Members.Select(m => (m.FirstName, m.LastName))
            .Should().BeEquivalentTo([("Ada", "Lovelace"), ("Grace", "Hopper")]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyEnabledRules_AndTheSeasonsRoundRange()
    {
        // Arrange - one enabled boost and one disabled, so the scoping filter is exercised.
        var world = await ArrangeAsync();
        var disabledId = await Seed.AddBoostDefinitionAsync("SHIELD", "Shield");
        await Seed.AddLeagueBoostRuleAsync(world.LeagueId, disabledId, totalUsesPerSeason: 1, isEnabled: false);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.BoostRules.Select(r => r.BoostCode).Should().BeEquivalentTo(["DOUBLE_UP"]);
        data.SeasonId.Should().Be(world.SeasonId);
        data.RoundRange.Should().NotBeNull();
        data.RoundRange!.MinRoundNumber.Should().Be(world.ClosedRoundNumber);
        data.RoundRange.MaxRoundNumber.Should().Be(world.OpenRoundNumber);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportTheInProgressAndLastCompletedRounds_WhenTheSeasonHasThem()
    {
        // Arrange - these two lookups drive whether a boost window counts as passed.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE_UP", "Double Up");
        await Seed.AddLeagueBoostRuleAsync(leagueId, boostId);

        await Seed.AddRoundAsync(backdrop.SeasonId, 3, DateTime.UtcNow.AddDays(-20), RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 4, DateTime.UtcNow.AddDays(-10), RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 5, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert - earliest in progress, latest completed.
        data!.InProgressRoundNumber.Should().Be(5);
        data.LastCompletedRoundNumber.Should().Be(4);
    }

    private async Task<BoostWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var opponentUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, opponentUserId);

        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE_UP", "Double Up");
        await Seed.AddLeagueBoostRuleAsync(leagueId, boostId, totalUsesPerSeason: 2);

        var closedDeadline = DateTime.UtcNow.AddDays(-7);
        var openDeadline = DateTime.UtcNow.AddDays(3);
        var closedRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 7, closedDeadline);
        var openRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 8, openDeadline);

        foreach (var userId in new[] { backdrop.UserId, opponentUserId })
        {
            await Seed.AddBoostUsageAsync(userId, leagueId, backdrop.SeasonId, closedRoundId, boostId);
            await Seed.AddBoostUsageAsync(userId, leagueId, backdrop.SeasonId, openRoundId, boostId);
        }

        // Only the closed round has been scored.
        await Seed.AddLeagueRoundResultAsync(leagueId, closedRoundId, backdrop.UserId, 9, 18, "DOUBLE_UP");

        return new BoostWorld(
            leagueId, backdrop.SeasonId, backdrop.UserId, opponentUserId,
            OpenRoundNumber: 8, ClosedRoundNumber: 7, openDeadline, closedDeadline);
    }

    private sealed record BoostWorld(
        int LeagueId,
        int SeasonId,
        string MeUserId,
        string OpponentUserId,
        int OpenRoundNumber,
        int ClosedRoundNumber,
        DateTime OpenRoundDeadlineUtc,
        DateTime ClosedRoundDeadlineUtc);
}
