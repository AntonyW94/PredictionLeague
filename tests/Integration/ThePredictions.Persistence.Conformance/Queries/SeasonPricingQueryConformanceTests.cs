using FluentAssertions;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ISeasonPricingQuery"/> implementation must return: every season with what the pricing rules judge it
/// on, and how many players took part in one of them.
/// </summary>
/// <remarks>
/// No horizon, no exclusion of free seasons, and no picking of the most recent one. All three were in the statement, and the
/// last one was measured against the database's clock.
/// </remarks>
public abstract class SeasonPricingQueryConformanceTests
{
    protected abstract ISeasonPricingQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task GetSeasonsAsync_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        (await Query.GetSeasonsAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetSeasonsAsync_ShouldReturnEachSeasonsCompetitionLengthAndDates()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var season = (await Query.GetSeasonsAsync(CancellationToken.None)).Single();

        // Assert
        season.Id.Should().Be(backdrop.SeasonId);
        season.CompetitionId.Should().Be(backdrop.CompetitionId);
        season.NumberOfRounds.Should().Be(38);
        season.StartDateUtc.Should().NotBe(default);
        season.EndDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task GetSeasonsAsync_ShouldReturnAFreeSeasonWithNoPrice()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var season = (await Query.GetSeasonsAsync(CancellationToken.None)).Single();

        // Assert - whether a free season shares the annual costs is a rule, so the price arrives as stored.
        season.StandardPrice.Should().BeNull();
    }

    [Fact]
    public async Task GetSeasonsAsync_ShouldReturnEverySeasonWhateverItsDatesOrCompetition()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2020/21");

        var otherCompetitionId = await Seed.AddCompetitionAsync("CUP");
        var cupSeasonId = await Seed.AddSeasonAsync(otherCompetitionId, "Cup 2026");

        // Act
        var seasons = await Query.GetSeasonsAsync(CancellationToken.None);

        // Assert - which of them share the costs, and which is the comparable one, are both rules.
        seasons.Select(season => season.Id)
            .Should().BeEquivalentTo([backdrop.SeasonId, otherSeasonId, cupSeasonId]);
    }

    [Fact]
    public async Task CountApprovedParticipantsAsync_ShouldCountNobody_ForASeasonWithNoLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var count = await Query.CountApprovedParticipantsAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task CountApprovedParticipantsAsync_ShouldCountEachPlayerOnce_HoweverManyLeaguesTheyAreIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var firstLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "First");
        var secondLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Second");

        await Seed.AddLeagueMemberAsync(firstLeagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(secondLeagueId, backdrop.UserId);

        // Act
        var count = await Query.CountApprovedParticipantsAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert - somebody in two leagues of the same season is one participant.
        count.Should().Be(1);
    }

    [Fact]
    public async Task CountApprovedParticipantsAsync_ShouldNotCountAnUnapprovedMembership()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var count = await Query.CountApprovedParticipantsAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert - somebody who asked to join and was never accepted did not take part.
        count.Should().Be(1);
    }

    [Fact]
    public async Task CountApprovedParticipantsAsync_ShouldNotCountAnotherSeasonsParticipants()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var otherLeagueId = await Seed.AddLeagueAsync(otherSeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(otherLeagueId, backdrop.UserId);

        // Act
        var count = await Query.CountApprovedParticipantsAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }
}
