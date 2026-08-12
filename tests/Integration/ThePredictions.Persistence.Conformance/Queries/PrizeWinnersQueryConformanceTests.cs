using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IPrizeWinnersQuery"/> implementation must return.
///
/// Three sets that the handler matches up: everything won across the round's season, everything already emailed about, and what
/// each round of the season is called. None of them is filtered or joined to another here, because deciding which winning a
/// sent-log row refers to is a rule.
/// </summary>
public abstract class PrizeWinnersQueryConformanceTests
{
    private static readonly DateTime DeadlineUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IPrizeWinnersQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereIsNoSuchRound()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.RoundId + 5_000, CancellationToken.None);

        // Assert
        data.Winnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachWinningWithItsPrizeAndWinner()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(
            world.LeagueId, PrizeType.Overall, 100m, prizeDescription: "1st Place");
        await Seed.AddWinningAsync(world.UserId, settingId, 100m);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        var winning = data.Winnings.Single();
        winning.UserId.Should().Be(world.UserId);
        winning.Email.Should().NotBeNullOrWhiteSpace();
        winning.FirstName.Should().Be("Ada");
        winning.LeagueId.Should().Be(world.LeagueId);
        winning.LeagueName.Should().Be("Integration League");
        winning.LeaguePrizeSettingId.Should().Be(settingId);
        winning.PrizeType.Should().Be(PrizeType.Overall);
        winning.PrizeDescription.Should().Be("1st Place");
        winning.Rank.Should().Be(1);
        winning.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWordingForAPrizeNobodyNamed()
    {
        // Arrange - the column allows null, and the email falls back to the prize type.
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);
        await Seed.AddWinningAsync(world.UserId, settingId, 5m, roundNumber: 5);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.Winnings.Single().PrizeDescription.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAWinningWorthNothing()
    {
        // Whether a prize of zero is worth an email is a rule, so the read must not decide it by leaving the row out.
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 0m);
        await Seed.AddWinningAsync(world.UserId, settingId, 0m, roundNumber: 5);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.Winnings.Single().Amount.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheScopeOfARoundPrize()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);
        await Seed.AddWinningAsync(world.UserId, settingId, 5m, roundNumber: 3);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert - the round number is what the sent-log has to be matched on, and a round prize has no month.
        var winning = data.Winnings.Single();
        winning.RoundNumber.Should().Be(3);
        winning.Month.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheScopeOfAMonthlyPrize()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Monthly, 20m);
        await Seed.AddWinningAsync(world.UserId, settingId, 20m, month: 3);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        var winning = data.Winnings.Single();
        winning.Month.Should().Be(3);
        winning.RoundNumber.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheScopeOfASeasonLongPrizeAsNeither()
    {
        // Arrange - and this is the pair of nulls that SQL will not treat as equal to another pair of nulls.
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m);
        await Seed.AddWinningAsync(world.UserId, settingId, 100m);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        var winning = data.Winnings.Single();
        winning.RoundNumber.Should().BeNull();
        winning.Month.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWinningsFromEveryLeagueInTheSeason()
    {
        // Arrange - a round belongs to a season, and every league running that season pays its own prizes for it.
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var mineId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m);
        var theirsId = await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 50m);
        await Seed.AddWinningAsync(world.UserId, mineId, 100m);
        await Seed.AddWinningAsync(world.UserId, theirsId, 50m);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.Winnings.Select(winning => winning.LeagueName)
            .Should().BeEquivalentTo(["Integration League", "Other League"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWinningsFromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2025/26", isActive: false);
        var otherLeagueId = await Seed.AddLeagueAsync(otherSeasonId, world.UserId, "Last Season");
        var settingId = await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 100m);
        await Seed.AddWinningAsync(world.UserId, settingId, 100m);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.Winnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryNotificationAlreadySentForTheSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);
        await Seed.AddWinningAsync(world.UserId, settingId, 5m, roundNumber: 5);
        await Seed.AddPrizeNotificationAsync(world.UserId, settingId, roundNumber: 3);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert - the round it was sent for comes back rather than being matched here, because that match is a rule.
        var notification = data.Notifications.Single();
        notification.UserId.Should().Be(world.UserId);
        notification.LeaguePrizeSettingId.Should().Be(settingId);
        notification.RoundNumber.Should().Be(3);
        notification.Month.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnANotificationWithNoRoundAndNoMonth()
    {
        // Arrange - a season-long prize is notified once, with neither part of the scope set.
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m);
        await Seed.AddWinningAsync(world.UserId, settingId, 100m);
        await Seed.AddPrizeNotificationAsync(world.UserId, settingId);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        var notification = data.Notifications.Single();
        notification.RoundNumber.Should().BeNull();
        notification.Month.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeasonWithItsName()
    {
        // Arrange - a prize won in an earlier round is named after that round, so every round's name is needed.
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 3, DeadlineUtc.AddDays(-7), displayName: "Gameweek 3");
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);
        await Seed.AddWinningAsync(world.UserId, settingId, 5m, roundNumber: 3);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.SeasonRounds.Should().Contain(round => round.RoundNumber == 3 && round.DisplayName == "Gameweek 3");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnABlankRoundNameAsItIsStored()
    {
        // Arrange - naming an unnamed round by its number is a rule, so the read hands back the blank.
        var world = await ArrangeAsync(roundDisplayName: string.Empty);
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 5m);
        await Seed.AddWinningAsync(world.UserId, settingId, 5m, roundNumber: 5);

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.SeasonRounds.Single(round => round.RoundNumber == 5).DisplayName.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReadTheOtherSets_WhenNothingWasWon()
    {
        // Arrange - with nothing won there is nothing to match up.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.RoundId, CancellationToken.None);

        // Assert
        data.Winnings.Should().BeEmpty();
        data.Notifications.Should().BeEmpty();
        data.SeasonRounds.Should().BeEmpty();
    }

    private async Task<PrizeWinnersWorld> ArrangeAsync(string? roundDisplayName = "Gameweek 5")
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, hasPrizes: true);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 5, DeadlineUtc, displayName: roundDisplayName);

        return new PrizeWinnersWorld(roundId, leagueId, backdrop.SeasonId, backdrop.CompetitionId, backdrop.UserId);
    }

    private sealed record PrizeWinnersWorld(int RoundId, int LeagueId, int SeasonId, int CompetitionId, string UserId);
}
