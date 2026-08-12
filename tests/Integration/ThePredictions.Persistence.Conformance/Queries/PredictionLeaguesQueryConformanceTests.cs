using FluentAssertions;
using ThePredictions.Application.Features.Predictions.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IPredictionLeaguesQuery"/> implementation must return: the leagues a player is in for a season, the
/// boost rules those leagues run, and the boosts the player has already spent - with none of the deciding done.
/// </summary>
public abstract class PredictionLeaguesQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IPredictionLeaguesQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThePlayerIsInNoLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None);

        // Assert
        data.Leagues.Should().BeEmpty();
        data.BoostRules.Should().BeEmpty();
        data.BoostUsages.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesTheyBelongTo()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Alpha League");

        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None)).Leagues.Single();

        // Assert
        league.LeagueId.Should().Be(leagueId);
        league.Name.Should().Be("Alpha League");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueTheyWereNotApprovedFor()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None);

        // Assert - a prediction does not count towards a league they have not been let into.
        data.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueFromAnotherSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var otherLeagueId = await Seed.AddLeagueAsync(otherSeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(otherLeagueId, backdrop.UserId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None);

        // Assert
        data.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnABoostRuleThatIsSwitchedOff()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddLeagueBoostRuleAsync(leagueId, boostId, totalUsesPerSeason: 2, isEnabled: false);

        // Act
        var rule = (await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None)).BoostRules.Single();

        // Assert - a league with rules that are all off is not the same as one with no rules, and telling those apart is a
        // rule, so the read must not filter.
        rule.LeagueId.Should().Be(leagueId);
        rule.BoostDefinitionId.Should().Be(boostId);
        rule.IsEnabled.Should().BeFalse();
        rule.TotalUsesPerSeason.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryBoostRuleOfEveryLeagueTheyAreIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var firstLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "First");
        var secondLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Second");

        await Seed.AddLeagueMemberAsync(firstLeagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(secondLeagueId, backdrop.UserId);

        var doubleId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        var bankerId = await Seed.AddBoostDefinitionAsync("BANKER", "Banker");

        await Seed.AddLeagueBoostRuleAsync(firstLeagueId, doubleId);
        await Seed.AddLeagueBoostRuleAsync(firstLeagueId, bankerId);
        await Seed.AddLeagueBoostRuleAsync(secondLeagueId, doubleId);

        // Act
        var rules = (await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None)).BoostRules;

        // Assert - each boost is judged separately, so every rule has to arrive.
        rules.Should().HaveCount(3);
        rules.Count(rule => rule.LeagueId == firstLeagueId).Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnBoostRulesOfALeagueTheyAreNotIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var theirLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, otherUserId, "Theirs");
        await Seed.AddLeagueMemberAsync(theirLeagueId, otherUserId);

        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddLeagueBoostRuleAsync(theirLeagueId, boostId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None);

        // Assert
        data.BoostRules.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachBoostUsedWithTheRoundItWasUsedIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddLeagueBoostRuleAsync(leagueId, boostId);

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddBoostUsageAsync(backdrop.UserId, leagueId, backdrop.SeasonId, roundId, boostId);

        // Act
        var usage = (await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None)).BoostUsages.Single();

        // Assert - the round is here because the same rows answer both "is one left this season" and "which is picked for
        // this round", and only the second cares which round it was.
        usage.LeagueId.Should().Be(leagueId);
        usage.BoostDefinitionId.Should().Be(boostId);
        usage.RoundId.Should().Be(roundId);
        usage.BoostCode.Should().Be("DOUBLE");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUsagesFromEveryRoundOfTheSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var doubleId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        var bankerId = await Seed.AddBoostDefinitionAsync("BANKER", "Banker");

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var secondRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddBoostUsageAsync(backdrop.UserId, leagueId, backdrop.SeasonId, firstRoundId, doubleId);
        await Seed.AddBoostUsageAsync(backdrop.UserId, leagueId, backdrop.SeasonId, secondRoundId, bankerId);

        // Act
        var usages = (await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None)).BoostUsages;

        // Assert - what is left for the season is judged across all of them, not only the round being predicted.
        usages.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherPlayersBoostUsage()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId);

        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddLeagueBoostRuleAsync(leagueId, boostId);

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddBoostUsageAsync(otherUserId, leagueId, backdrop.SeasonId, roundId, boostId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None);

        // Assert - somebody else spending their boost must not spend yours.
        data.BoostUsages.Should().BeEmpty();
    }
}
