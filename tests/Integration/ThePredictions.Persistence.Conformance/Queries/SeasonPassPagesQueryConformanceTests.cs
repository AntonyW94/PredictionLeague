using FluentAssertions;
using ThePredictions.Application.Features.SeasonPasses.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ISeasonPassPagesQuery"/> implementation must return: the seasons, their leagues' entry deadlines,
/// how many are taking part in each, and the passes one player holds.
///
/// None of the four screens' conditions are applied here. In particular the deadlines come back as dates rather than as a
/// yes-or-no answer, because "still open" is measured against the injected clock and three of the statements this replaces
/// called <c>GETUTCDATE()</c> instead.
/// </summary>
public abstract class SeasonPassPagesQueryConformanceTests
{
    private const string NoSuchUser = "no-such-user";

    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract ISeasonPassPagesQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        // Act
        var data = await Query.ExecuteAsync(NoSuchUser, CancellationToken.None);

        // Assert
        data.Seasons.Should().BeEmpty();
        data.Leagues.Should().BeEmpty();
        data.HolderCounts.Should().BeEmpty();
        data.HeldPasses.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachSeasonWithItsPricesAndCompetition()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var season = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Seasons.Single();

        // Assert - the prices arrive as stored; deciding that a season with no price is a free one is a rule.
        season.Id.Should().Be(backdrop.SeasonId);
        season.Name.Should().Be("2026/27");
        season.IsActive.Should().BeTrue();
        season.StartDateUtc.Should().NotBe(default);
        season.StandardPrice.Should().BeNull();
        season.PremiumPrice.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnASeasonThatHasBeenRetired()
    {
        // Arrange - the options page is reached by id and has to answer for one of these.
        var backdrop = await Seed.AddBackdropAsync();
        var retiredSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2024/25", isActive: false);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - whether an inactive season may be offered is a rule.
        data.Seasons.Single(season => season.Id == retiredSeasonId).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachLeaguesEntryDeadlineRatherThanWhetherItIsOpen()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        // Act
        var league = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).Leagues.Single();

        // Assert - a date, so the handler can measure it against the clock it was given.
        league.SeasonId.Should().Be(backdrop.SeasonId);
        league.LeagueId.Should().Be(leagueId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryLeagueOfEverySeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var firstLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "This Season");
        var secondLeagueId = await Seed.AddLeagueAsync(otherSeasonId, backdrop.UserId, "Next Season");

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the pages show every season at once, and whether a season has any league at all is one of the rules.
        data.Leagues.Select(league => league.LeagueId).Should().BeEquivalentTo([firstLeagueId, secondLeagueId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountEverybodyTakingPartInEachSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId, SeasonPassTier.Standard, SeasonPassSource.Trial);

        // Act
        var count = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).HolderCounts.Single();

        // Assert - counted from the passes rather than from league membership, so a trial counts and a player who has not
        // picked a league yet counts exactly once.
        count.SeasonId.Should().Be(backdrop.SeasonId);
        count.HolderCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoCount_ForASeasonNobodyHasTakenUp()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(NoSuchUser, CancellationToken.None);

        // Assert - an absent row rather than a zero, and reading that as "nobody" is the handler's job.
        data.HolderCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyThePassesThisPlayerHolds()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId, SeasonPassTier.Premium, SeasonPassSource.Purchased);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var pass = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).HeldPasses.Single();

        // Assert - the tier arrives as stored, because reading the premium tier as "carries text-message reminders" is a
        // rule. Trial eligibility is decided from this set being empty, so it must hold every season, not just one.
        pass.SeasonId.Should().Be(backdrop.SeasonId);
        pass.Tier.Should().Be(nameof(SeasonPassTier.Premium));
        pass.Source.Should().Be(nameof(SeasonPassSource.Purchased));
        pass.CreatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEverySeasonThePlayerHoldsAPassFor()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var secondSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(backdrop.UserId, secondSeasonId);

        // Act
        var passes = (await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None)).HeldPasses;

        // Assert - a player who has ever held one is not eligible for a trial, so all of them have to arrive.
        passes.Select(pass => pass.SeasonId).Should().BeEquivalentTo([backdrop.SeasonId, secondSeasonId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPasses_ForAPlayerWhoHoldsNone()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(NoSuchUser, CancellationToken.None);

        // Assert
        data.HeldPasses.Should().BeEmpty();
    }
}
