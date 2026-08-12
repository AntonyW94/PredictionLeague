using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IPrizeEvaluationInputsQuery"/> implementation must return.
///
/// A league is reached two ways - by its id from its own pages, and by its entry code from the join flow - and both must produce
/// the same answer about the same league. That is why they are asserted side by side here: they used to be one projection with a
/// predicate concatenated onto it, and now they are two statements that have to be kept saying the same thing.
/// </summary>
public abstract class PrizeEvaluationInputsQueryConformanceTests
{
    private static readonly DateTime EntryDeadlineUtc = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IPrizeEvaluationInputsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region No such league

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNull_WhenThereIsNoSuchLeague()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId + 5_000, CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    [Fact]
    public async Task GetByEntryCodeAsync_ShouldReturnNull_WhenNoLeagueHasThatCode()
    {
        // Arrange
        await ArrangeAsync();

        // Act
        var data = await Query.GetByEntryCodeAsync("NOSUCH", CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    #endregion

    #region The league and its season

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnThePotContext()
    {
        // Arrange
        var world = await ArrangeAsync(price: 10m, prizeFundOverride: 25m);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert - the stake, the administrator's top-up and the number of people in are what the pot is made of.
        var league = data!.League;
        league.LeagueId.Should().Be(world.LeagueId);
        league.LeagueName.Should().Be("Integration League");
        league.EntryCost.Should().Be(10m);
        league.PrizeFundOverride.Should().Be(25m);
        league.EntrantCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNoTopUp_WhenTheAdministratorHasNotAddedOne()
    {
        // Arrange - the column allows null, and no top-up is not the same fact as a top-up of nothing.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.League.PrizeFundOverride.Should().BeNull();
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldCountOnlyApprovedMembersAsEntries()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert - the entrant count multiplies into every prize, so a request to join must not inflate it.
        data!.League.EntrantCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnTheSeasonsShape()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert - how many rounds and how long the season runs decide how many round and monthly prizes there are.
        var league = data!.League;
        league.SeasonName.Should().Be("2026/27");
        league.NumberOfRounds.Should().Be(38);
        league.SeasonStartDateUtc.Should().NotBe(default);
        league.SeasonEndDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnTheAdministratorsNameInParts()
    {
        // Arrange - abbreviating a name is a rule, so both parts arrive raw.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var league = data!.League;
        league.AdministratorUserId.Should().Be(world.UserId);
        league.AdministratorFirstName.Should().Be("Ada");
        league.AdministratorLastName.Should().Be("Lovelace");
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange - the column allows it, and whether entry is still open is decided from this.
        var world = await ArrangeAsync(entryDeadlineUtc: null);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.League.EntryDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnTheEntryDeadline_WhenTheLeagueHasOne()
    {
        // Arrange
        var world = await ArrangeAsync(entryDeadlineUtc: EntryDeadlineUtc);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.League.EntryDeadlineUtc.Should().Be(EntryDeadlineUtc);
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNoEntryCode_ForAPublicLeague()
    {
        // Arrange - whether a league is private is worked out from whether it has a code.
        var world = await ArrangeAsync(entryCode: null);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.League.EntryCode.Should().BeNull();
    }

    #endregion

    #region The scheme

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNoScheme_WhenTheLeagueHasNotSetOne()
    {
        // Arrange - whether the league has a scheme is the caller's judgement, made from whether this set is empty.
        var world = await ArrangeAsync();

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Schemes.Should().BeEmpty();
        data.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnTheSchemeWithNoCategories_WhenNoneAreEnabled()
    {
        // Arrange - a scheme with nothing in it is a real state, and it is not the same as having no scheme.
        var world = await ArrangeAsync();
        await Seed.AddLeaguePrizeSchemeAsync(world.LeagueId, world.UserId);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Schemes.Should().ContainSingle();
        data.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnEachCategoryWithItsShareOfAnEntry()
    {
        // Arrange
        var world = await ArrangeAsync();
        var schemeId = await Seed.AddLeaguePrizeSchemeAsync(world.LeagueId, world.UserId);
        await Seed.AddLeaguePrizeSchemeEntryAsync(schemeId, PrizeType.Overall, 6);
        await Seed.AddLeaguePrizeSchemeEntryAsync(schemeId, PrizeType.Round, 3);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Entries.Select(entry => (entry.Category, entry.PerEntryPounds))
            .Should().BeEquivalentTo([(PrizeType.Overall, 6), (PrizeType.Round, 3)]);
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnACategorysOwnPlacesTable()
    {
        // Arrange - a league can override how far down the places pay, and it is stored as text.
        var world = await ArrangeAsync();
        var schemeId = await Seed.AddLeaguePrizeSchemeAsync(world.LeagueId, world.UserId);
        await Seed.AddLeaguePrizeSchemeEntryAsync(schemeId, PrizeType.Overall, 6, rankTableJson: "[60,30,10]");

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Entries.Single().RankTableJson.Should().Be("[60,30,10]");
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNoPlacesTable_WhenTheCategoryUsesTheDefault()
    {
        // Arrange - the column allows null, and null means "use the product default" rather than "pays nobody".
        var world = await ArrangeAsync();
        var schemeId = await Seed.AddLeaguePrizeSchemeAsync(world.LeagueId, world.UserId);
        await Seed.AddLeaguePrizeSchemeEntryAsync(schemeId, PrizeType.Overall, 6);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Entries.Single().RankTableJson.Should().BeNull();
    }

    [Fact]
    public async Task GetByLeagueIdAsync_ShouldReturnNoSchemeBelongingToAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var otherSchemeId = await Seed.AddLeaguePrizeSchemeAsync(otherLeagueId, world.UserId);
        await Seed.AddLeaguePrizeSchemeEntryAsync(otherSchemeId, PrizeType.Overall, 6);

        // Act
        var data = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Schemes.Should().BeEmpty();
        data.Entries.Should().BeEmpty();
    }

    #endregion

    #region Reached by entry code

    [Fact]
    public async Task GetByEntryCodeAsync_ShouldReturnTheSameLeagueAsItsId()
    {
        // The two entry points were one projection with a predicate stuck on the end; they are two statements now, and the
        // whole point is that they still describe the same league.
        var world = await ArrangeAsync(entryCode: "ABC123", price: 10m);
        var schemeId = await Seed.AddLeaguePrizeSchemeAsync(world.LeagueId, world.UserId);
        await Seed.AddLeaguePrizeSchemeEntryAsync(schemeId, PrizeType.Overall, 6, rankTableJson: "[100]");

        // Act
        var byCode = await Query.GetByEntryCodeAsync("ABC123", CancellationToken.None);
        var byId = await Query.GetByLeagueIdAsync(world.LeagueId, CancellationToken.None);

        // Assert
        byCode.Should().BeEquivalentTo(byId);
    }

    [Fact]
    public async Task GetByEntryCodeAsync_ShouldReturnTheLeagueWithThatCodeAndNoOther()
    {
        // Arrange
        var world = await ArrangeAsync(entryCode: "ABC123");
        var otherLeagueId = await Seed.AddLeagueAsync(
            world.SeasonId, world.UserId, "Other League", entryCode: "XYZ789");
        await Seed.AddLeagueMemberAsync(otherLeagueId, world.UserId);

        // Act
        var data = await Query.GetByEntryCodeAsync("XYZ789", CancellationToken.None);

        // Assert
        data!.League.LeagueId.Should().Be(otherLeagueId);
        data.League.LeagueName.Should().Be("Other League");
    }

    #endregion

    private async Task<PrizeInputsWorld> ArrangeAsync(
        DateTime? entryDeadlineUtc = null,
        string? entryCode = null,
        decimal price = 0m,
        decimal? prizeFundOverride = null)
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(
            backdrop.SeasonId, backdrop.UserId, hasPrizes: true,
            entryDeadlineUtc: entryDeadlineUtc, entryCode: entryCode, price: price,
            prizeFundOverride: prizeFundOverride);

        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new PrizeInputsWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record PrizeInputsWorld(int LeagueId, int SeasonId, string UserId);
}
