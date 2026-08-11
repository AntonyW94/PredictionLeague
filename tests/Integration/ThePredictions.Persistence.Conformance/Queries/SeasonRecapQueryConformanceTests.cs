using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ISeasonRecapQuery"/> implementation must return.
///
/// The recap is about one player, but three of its answers can only be worked out against the rest of the league,
/// so the split matters: members and scores come back whole, while winnings and exact scores are the player's
/// alone. An adapter that narrowed the scores to the player would leave the final position, the rounds won and the
/// position trajectory impossible to compute.
/// </summary>
public abstract class SeasonRecapQueryConformanceTests
{
    protected abstract ISeasonRecapQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId + 5_000, world.UserId, CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesPriceAndWhetherItIsFree()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert - the entry fee is half of the profit-or-loss rule.
        data!.IsFree.Should().BeTrue();
        data.LeaguePrice.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryApprovedMember_NotJustThePlayer()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var pendingId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert - the player's final position is against these, and the count is the league's size.
        data!.ApprovedMembers.Select(member => member.UserId)
            .Should().BeEquivalentTo([world.UserId, rivalId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeason_WhateverItsStatus()
    {
        // Arrange
        var world = await ArrangeAsync();
        var completedId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        var liveId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);
        var futureId = await Seed.AddRoundAsync(world.SeasonId, 3, DateTime.UtcNow.AddDays(7));

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert - the trajectory walks the completed ones, the best and worst rounds ignore status entirely, so
        // filtering here would settle a rule the handler has to make.
        data!.SeasonRounds.Select(round => round.RoundId)
            .Should().BeEquivalentTo([completedId, liveId, futureId]);
        data.SeasonRounds.Single(round => round.RoundId == liveId).Status.Should().Be(RoundStatus.InProgress);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsNumberAndStartDate()
    {
        // Arrange
        var world = await ArrangeAsync();
        var startDateUtc = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
        var roundId = await Seed.AddRoundAsync(
            world.SeasonId, 6, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed, startDateUtc);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert - the number orders the trajectory, the start date places a round in a calendar month.
        var round = data!.SeasonRounds.Single(candidate => candidate.RoundId == roundId);
        round.RoundNumber.Should().Be(6);
        round.StartDateUtc.Should().BeCloseTo(startDateUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoRounds_FromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2027/28");
        await Seed.AddRoundAsync(otherSeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.SeasonRounds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryMembersScores_NotJustThePlayers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, roundId, world.UserId, 9, 18, "DOUBLE");
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, roundId, rivalId, 7, 7, "NONE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert - a recap cannot say where someone finished without the people they finished ahead of.
        data!.RoundScores.Should().HaveCount(2);
        data.RoundScores.Single(score => score.UserId == rivalId).BoostedPoints.Should().Be(7);
        data.RoundScores.Single(score => score.UserId == world.UserId).RoundId.Should().Be(roundId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnScoresForThisLeagueOnly()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        await Seed.AddLeagueMemberAsync(otherLeagueId, world.UserId);
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(otherLeagueId, roundId, world.UserId, 40, 80, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.RoundScores.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnThePlayersExactScoresUnaggregated()
    {
        // Arrange
        var world = await ArrangeAsync();
        var firstRoundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        var secondRoundId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundResultAsync(firstRoundId, world.UserId, exactScoreCount: 3);
        await Seed.AddRoundResultAsync(secondRoundId, world.UserId, exactScoreCount: 1);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert - totalling them is the rule.
        data!.ExactScoreCounts.Should().BeEquivalentTo([3, 1]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoExactScores_ForAnotherPlayer()
    {
        // Arrange - the recap never compares exact scores with anyone, so this read is the player's alone.
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundResultAsync(roundId, rivalId, exactScoreCount: 9);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.ExactScoreCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoExactScores_FromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2027/28");
        var otherRoundId = await Seed.AddRoundAsync(otherSeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundResultAsync(otherRoundId, world.UserId, exactScoreCount: 9);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.ExactScoreCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnThePlayersWinningsUnaggregated()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 10m);
        await Seed.AddWinningAsync(world.UserId, settingId, 10m, roundNumber: 1);
        await Seed.AddWinningAsync(world.UserId, settingId, 15m, roundNumber: 2);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.WinningAmounts.Should().BeEquivalentTo([10m, 15m]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWinnings_ForAnotherPlayer()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m);
        await Seed.AddWinningAsync(rivalId, settingId, 100m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.WinningAmounts.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWinnings_FromAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var settingId = await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 500m);
        await Seed.AddWinningAsync(world.UserId, settingId, 500m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.UserId, CancellationToken.None);

        // Assert
        data!.WinningAmounts.Should().BeEmpty();
    }

    private async Task<RecapWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new RecapWorld(leagueId, backdrop.CompetitionId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record RecapWorld(int LeagueId, int CompetitionId, int SeasonId, string UserId);
}
