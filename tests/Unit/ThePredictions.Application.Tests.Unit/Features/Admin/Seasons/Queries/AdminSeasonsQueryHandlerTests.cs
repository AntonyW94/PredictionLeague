using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Queries;

/// <summary>
/// The administrator's season screens: the list and the single season, which shared a twenty-column statement.
///
/// Most of these tests are about the counts on those screens - rounds by state, and how many teams are in the season -
/// because each of those was a correlated subquery with a rule inside it.
/// </summary>
public class AdminSeasonsQueryHandlerTests
{
    private const int SeasonId = 7;

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonsQuery _seasonsQuery = Substitute.For<ISeasonsQuery>();

    #region The list

    [Fact]
    public async Task FetchAll_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        // Arrange
        Given(new SeasonsData([], [], []));

        // Act
        var seasons = await FetchAllAsync();

        // Assert
        seasons.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAll_ShouldListTheNewestSeasonFirst()
    {
        // Arrange
        Given(Data(seasons:
        [
            Season(1, "2025/26") with { StartDateUtc = SeasonStart.AddYears(-1) },
            Season(2, "2027/28") with { StartDateUtc = SeasonStart.AddYears(1) },
            Season(3, "2026/27")
        ]));

        // Act
        var seasons = await FetchAllAsync();

        // Assert
        seasons.Select(season => season.Name).Should().Equal("2027/28", "2026/27", "2025/26");
    }

    [Fact]
    public async Task FetchAll_ShouldReportEachSeasonsDetails()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, "2026/27")]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.Id.Should().Be(SeasonId);
        season.Name.Should().Be("2026/27");
        season.StartDateUtc.Should().Be(SeasonStart);
        season.EndDateUtc.Should().Be(SeasonStart.AddMonths(10));
        season.IsActive.Should().BeTrue();
        season.NumberOfRounds.Should().Be(38);
        season.CompetitionId.Should().Be(100);
        season.CompetitionName.Should().Be("Premier League");
        season.CompetitionType.Should().Be(CompetitionType.League);
        season.ApiLeagueId.Should().Be(39);
        season.PassStandardPrice.Should().Be(10m);
        season.PassPremiumPrice.Should().Be(20m);
        season.PassHolderCount.Should().Be(12);
    }

    [Fact]
    public async Task FetchAll_ShouldReportAFreeSeasonWithNoPassPrices()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, "2026/27") with { PassStandardPrice = null, PassPremiumPrice = null }]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.PassStandardPrice.Should().BeNull();
        season.PassPremiumPrice.Should().BeNull();
    }

    #endregion

    #region Round counts

    [Fact]
    public async Task FetchAll_ShouldCountTheSeasonsRoundsByState()
    {
        // Each of these was its own correlated subquery with the state written in as a text literal.
        Given(Data(
            seasons: [Season(SeasonId, "2026/27")],
            rounds:
            [
                Round(1, RoundStatus.Completed),
                Round(2, RoundStatus.Completed),
                Round(3, RoundStatus.InProgress),
                Round(4, RoundStatus.Published),
                Round(5, RoundStatus.Draft)
            ]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.RoundCount.Should().Be(5);
        season.CompletedCount.Should().Be(2);
        season.InProgressCount.Should().Be(1);
        season.PublishedCount.Should().Be(1);
        season.DraftCount.Should().Be(1);
    }

    [Fact]
    public async Task FetchAll_ShouldCountNoRounds_ForASeasonThatHasNoneYet()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, "2026/27")]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.RoundCount.Should().Be(0);
        season.TeamCount.Should().Be(0);
    }

    [Fact]
    public async Task FetchAll_ShouldNotCountAnotherSeasonsRounds()
    {
        // Arrange
        Given(Data(
            seasons: [Season(SeasonId, "2026/27"), Season(8, "2027/28")],
            rounds: [Round(1, RoundStatus.Completed), Round(1, RoundStatus.Draft, seasonId: 8)]));

        // Act
        var seasons = await FetchAllAsync();

        // Assert
        seasons.Single(season => season.Id == SeasonId).CompletedCount.Should().Be(1);
        seasons.Single(season => season.Id == SeasonId).DraftCount.Should().Be(0);
        seasons.Single(season => season.Id == 8).DraftCount.Should().Be(1);
    }

    #endregion

    #region Team count

    [Fact]
    public async Task FetchAll_ShouldCountTheTeamsInTheSeasonsFirstRound()
    {
        // Arrange - two fixtures in round 1, so four teams.
        Given(Data(
            seasons: [Season(SeasonId, "2026/27")],
            rounds: [Round(1, RoundStatus.Completed), Round(2, RoundStatus.Published)],
            fixtures: [Fixture(1, 10, 20), Fixture(1, 30, 40), Fixture(2, 50, 60)]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert - the first round only. A later round's teams are the same teams in a league, and in a knockout they are
        // a subset - either way the entrants are decided by round one.
        season.TeamCount.Should().Be(4);
    }

    [Fact]
    public async Task FetchAll_ShouldCountEachTeamOnce_HoweverManyFixturesItHas()
    {
        // Arrange
        Given(Data(
            seasons: [Season(SeasonId, "2026/27")],
            rounds: [Round(1, RoundStatus.Completed)],
            fixtures: [Fixture(1, 10, 20), Fixture(1, 20, 10)]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.TeamCount.Should().Be(2);
    }

    [Fact]
    public async Task FetchAll_ShouldNotCountAFixtureWhoseTeamsAreNotKnownYet()
    {
        // A knockout round before its ties are settled holds placeholders rather than teams.
        Given(Data(
            seasons: [Season(SeasonId, "2026/27")],
            rounds: [Round(1, RoundStatus.Published)],
            fixtures: [Fixture(1, 10, 20), Fixture(1, null, null)]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.TeamCount.Should().Be(2);
    }

    [Fact]
    public async Task FetchAll_ShouldTakeTheFirstRoundByNumberRatherThanTheFirstRowSeen()
    {
        // Arrange - the rows deliberately arrive with the later round first.
        Given(Data(
            seasons: [Season(SeasonId, "2026/27")],
            rounds: [Round(9, RoundStatus.Published), Round(4, RoundStatus.Published)],
            fixtures: [Fixture(9, 10, 20), Fixture(4, 30, 40), Fixture(4, 50, 60)]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert - round 4 is the season's first, and it has four teams.
        season.TeamCount.Should().Be(4);
    }

    [Fact]
    public async Task FetchAll_ShouldNotCountAnotherSeasonsTeams()
    {
        // Arrange
        Given(Data(
            seasons: [Season(SeasonId, "2026/27")],
            rounds: [Round(1, RoundStatus.Published)],
            fixtures: [Fixture(1, 10, 20), Fixture(1, 30, 40, seasonId: 8)]));

        // Act
        var season = (await FetchAllAsync()).Single();

        // Assert
        season.TeamCount.Should().Be(2);
    }

    #endregion

    #region One season

    [Fact]
    public async Task GetById_ShouldReturnTheSeasonAskedForWithItsCounts()
    {
        // Arrange
        Given(Data(
            seasons: [Season(SeasonId, "2026/27"), Season(8, "2027/28")],
            rounds: [Round(1, RoundStatus.Completed)],
            fixtures: [Fixture(1, 10, 20)]));

        // Act
        var season = await new GetSeasonByIdQueryHandler(_seasonsQuery)
            .Handle(new GetSeasonByIdQuery(SeasonId), CancellationToken.None);

        // Assert
        season.Name.Should().Be("2026/27");
        season.CompletedCount.Should().Be(1);
        season.TeamCount.Should().Be(2);
    }

    [Fact]
    public async Task GetById_ShouldReportNotFound_WhenThereIsNoSuchSeason()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, "2026/27")]));

        // Act
        var act = () => new GetSeasonByIdQueryHandler(_seasonsQuery)
            .Handle(new GetSeasonByIdQuery(99), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    private void Given(SeasonsData data) =>
        _seasonsQuery.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(data);

    private static SeasonsData Data(
        AdminSeasonRow[]? seasons = null,
        SeasonRoundStatusRow[]? rounds = null,
        SeasonFixtureTeamsRow[]? fixtures = null) =>
        new(seasons ?? [], rounds ?? [], fixtures ?? []);

    private static AdminSeasonRow Season(int id, string name) =>
        new(id, name, SeasonStart, SeasonStart.AddMonths(10), IsActive: true, NumberOfRounds: 38,
            CompetitionId: 100, "Premier League", CompetitionType.League, ApiLeagueId: 39,
            PassStandardPrice: 10m, PassPremiumPrice: 20m, PassHolderCount: 12);

    private static SeasonRoundStatusRow Round(int roundNumber, RoundStatus status, int seasonId = SeasonId) =>
        new(seasonId, roundNumber, status);

    private static SeasonFixtureTeamsRow Fixture(int roundNumber, int? homeTeamId, int? awayTeamId, int seasonId = SeasonId) =>
        new(seasonId, roundNumber, homeTeamId, awayTeamId);

    private Task<IEnumerable<SeasonDto>> FetchAllAsync() =>
        new FetchAllSeasonsQueryHandler(_seasonsQuery).Handle(new FetchAllSeasonsQuery(), CancellationToken.None);
}
