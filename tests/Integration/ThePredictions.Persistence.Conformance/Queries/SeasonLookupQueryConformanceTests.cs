using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ISeasonLookupQuery"/> implementation must return.
///
/// Every season, active or not, with its competition type rather than a flag. Both of those were decided inside the old
/// statement - a <c>WHERE IsActive = 1</c> and a <c>CASE WHEN c.[Type] = 1</c> - and both are rules.
/// </summary>
public abstract class SeasonLookupQueryConformanceTests
{
    protected abstract ISeasonLookupQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        // Act
        var seasons = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        seasons.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachSeasonsNameStartDateAndCompetitionType()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var seasons = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        var season = seasons.Single(candidate => candidate.Id == backdrop.SeasonId);
        season.Name.Should().Be("2026/27");
        season.StartDateUtc.Should().NotBe(default);
        season.CompetitionType.Should().BeOneOf(CompetitionType.League, CompetitionType.Tournament);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhetherEachSeasonIsActive()
    {
        // Arrange - the seeded backdrop's season is active.
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var seasons = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - whether an inactive season may host a new league is the handler's rule, so the flag has to arrive
        // rather than being applied here.
        seasons.Single(candidate => candidate.Id == backdrop.SeasonId).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEverySeasonOfEveryCompetition()
    {
        // Arrange - two seasons of the same competition, plus one of another.
        var backdrop = await Seed.AddBackdropAsync();
        var secondSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var otherCompetitionId = await Seed.AddCompetitionAsync("OTHER");
        var otherSeasonId = await Seed.AddSeasonAsync(otherCompetitionId, "Cup 2026");

        // Act
        var seasons = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        seasons.Select(season => season.Id)
            .Should().BeEquivalentTo([backdrop.SeasonId, secondSeasonId, otherSeasonId]);
    }
}
