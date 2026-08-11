using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The create-a-league page: which seasons may host a new league, and the scoring it starts with.
/// </summary>
public class GetCreateLeaguePageDataQueryHandlerTests
{
    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonLookupQuery _seasonLookupQuery = Substitute.For<ISeasonLookupQuery>();
    private readonly GetCreateLeaguePageDataQueryHandler _handler;

    public GetCreateLeaguePageDataQueryHandlerTests()
    {
        _handler = new GetCreateLeaguePageDataQueryHandler(_seasonLookupQuery);
    }

    [Fact]
    public async Task Handle_ShouldOfferOnlyActiveSeasons()
    {
        // Arrange
        Given(
            Season(1, "Current", isActive: true),
            Season(2, "Finished", isActive: false));

        // Act
        var page = await HandleAsync();

        // Assert - a new league cannot be created in a season that is over.
        page.Seasons.Select(season => season.Name).Should().Equal("Current");
    }

    [Fact]
    public async Task Handle_ShouldOfferTheNewestSeasonFirst()
    {
        // Arrange
        Given(
            Season(1, "Older", startDateUtc: SeasonStart),
            Season(2, "Newer", startDateUtc: SeasonStart.AddYears(1)));

        // Act
        var page = await HandleAsync();

        // Assert
        page.Seasons.Select(season => season.Name).Should().Equal("Newer", "Older");
    }

    [Theory]
    [InlineData(CompetitionType.Tournament, true)]
    [InlineData(CompetitionType.League, false)]
    public async Task Handle_ShouldReportWhetherASeasonIsATournament(
        CompetitionType competitionType,
        bool expected)
    {
        // Arrange
        Given(Season(1, "Current", competitionType: competitionType));

        // Act
        var page = await HandleAsync();

        // Assert
        page.Seasons.Single().IsTournament.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldOfferTheDefaultScoring()
    {
        // Arrange
        Given(Season(1, "Current"));

        // Act
        var page = await HandleAsync();

        // Assert - the same points a public league uses, so a new league starts consistent with them.
        page.DefaultPointsForExactScore.Should().Be(PublicLeagueSettings.PointsForExactScore);
        page.DefaultPointsForCorrectResult.Should().Be(PublicLeagueSettings.PointsForCorrectResult);
    }

    [Fact]
    public async Task Handle_ShouldOfferNoSeasons_WhenNoneIsActive()
    {
        // Arrange
        Given(Season(1, "Finished", isActive: false));

        // Act
        var page = await HandleAsync();

        // Assert - and the defaults are still offered, so the page can render.
        page.Seasons.Should().BeEmpty();
        page.DefaultPointsForExactScore.Should().Be(PublicLeagueSettings.PointsForExactScore);
    }

    private void Given(params SeasonLookupRow[] seasons)
    {
        _seasonLookupQuery.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(seasons);
    }

    private async Task<CreateLeaguePageData> HandleAsync() =>
        await _handler.Handle(new GetCreateLeaguePageDataQuery(), CancellationToken.None);

    private static SeasonLookupRow Season(
        int id,
        string name,
        bool isActive = true,
        DateTime? startDateUtc = null,
        CompetitionType competitionType = CompetitionType.League) =>
        new(id, name, startDateUtc ?? SeasonStart, isActive, competitionType);
}
