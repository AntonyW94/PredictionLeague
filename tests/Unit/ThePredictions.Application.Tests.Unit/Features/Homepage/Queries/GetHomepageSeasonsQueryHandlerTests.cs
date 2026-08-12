using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Homepage.Queries;
using ThePredictions.Contracts.Homepage;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Homepage.Queries;

/// <summary>
/// The seasons the public homepage advertises.
///
/// The statement behind this called the database's clock three separate times - to decide which seasons to show, which were
/// under way and which were still to come - so a season could in principle have been described as neither. Everything is now
/// measured against one instant.
/// </summary>
public class GetHomepageSeasonsQueryHandlerTests
{
    private const int SeasonId = 7;

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IHomepageSeasonsQuery _query = Substitute.For<IHomepageSeasonsQuery>();
    private readonly GetHomepageSeasonsQueryHandler _handler;

    public GetHomepageSeasonsQueryHandlerTests()
    {
        _handler = new GetHomepageSeasonsQueryHandler(_query, new TestDateTimeProvider(Now));
        Given(new HomepageSeasonsData([], [], []));
    }

    #region Which seasons appear

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        (await HandleAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldShowASeasonThatIsUnderWay()
    {
        // Arrange
        Given(Data(Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.IsInProgress.Should().BeTrue();
        season.IsUpcoming.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldShowASeasonThatHasNotStarted()
    {
        // Arrange
        Given(Data(Season(SeasonId, Now.AddMonths(1), Now.AddMonths(10))));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.IsUpcoming.Should().BeTrue();
        season.IsInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotShowASeasonThatHasFinished()
    {
        // The homepage is an advert, and a finished competition advertises nothing.
        Given(Data(Season(SeasonId, Now.AddYears(-1), Now.AddDays(-1))));

        // Act
        var seasons = await HandleAsync();

        // Assert
        seasons.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldStillShowASeasonEndingToday()
    {
        // Inclusive: a season ending at this instant is still this season.
        Given(Data(Season(SeasonId, Now.AddYears(-1), Now)));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.IsInProgress.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldDescribeEverySeasonShownAsEitherUnderWayOrToCome()
    {
        // The three separate clock reads this replaces could in principle have disagreed with each other.
        Given(Data(
            Season(1, Now.AddMonths(-1), Now.AddMonths(9)),
            Season(2, Now.AddMonths(1), Now.AddMonths(10))));

        // Act
        var seasons = await HandleAsync();

        // Assert
        seasons.Should().OnlyContain(season => season.IsInProgress || season.IsUpcoming);
    }

    [Fact]
    public async Task Handle_ShouldListTheEarliestStartingSeasonFirst()
    {
        // Arrange
        Given(Data(
            Season(1, Now.AddMonths(2), Now.AddMonths(11)),
            Season(2, Now.AddMonths(-1), Now.AddMonths(9))));

        // Act
        var seasons = await HandleAsync();

        // Assert
        seasons.Select(season => season.Id).Should().Equal(2, 1);
    }

    #endregion

    #region What each season shows

    [Fact]
    public async Task Handle_ShouldReportEachSeasonsNameDatesAndCompetitionType()
    {
        // Arrange
        Given(Data(Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.Id.Should().Be(SeasonId);
        season.Name.Should().Be("2026/27");
        season.CompetitionType.Should().Be(CompetitionType.League);
        season.StartDateUtc.Should().Be(Now.AddMonths(-1));
        season.EndDateUtc.Should().Be(Now.AddMonths(9));
    }

    [Fact]
    public async Task Handle_ShouldCountTheSeasonsLeagues()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))],
            leagues: [League(1, price: 10m, memberCount: 4), League(2, price: 5m, memberCount: 2)]));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.LeagueCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAnotherSeasonsLeagues()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))],
            leagues: [League(1, price: 10m, memberCount: 4), League(2, price: 5m, memberCount: 2, seasonId: 99)]));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.LeagueCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountAPlayerOnce_HoweverManyOfTheSeasonsLeaguesTheyAreIn()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))],
            memberships:
            [
                new HomepageMembershipRow(SeasonId, "u1"),
                new HomepageMembershipRow(SeasonId, "u1"),
                new HomepageMembershipRow(SeasonId, "u2")
            ]));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.PlayerCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAnotherSeasonsPlayers()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))],
            memberships: [new HomepageMembershipRow(SeasonId, "u1"), new HomepageMembershipRow(99, "u2")]));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.PlayerCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldAddUpThePrizeFundAcrossTheSeasonsLeagues()
    {
        // Entry fee times members, plus whatever each administrator has put in - the same rule a single league's page uses, and
        // the third place it was found written out in SQL.
        Given(Data(seasons: [Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))],
            leagues:
            [
                League(1, price: 10m, memberCount: 4),
                League(2, price: 5m, memberCount: 2, prizeFundOverride: 25m)
            ]));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert - 40 plus 10 plus 25.
        season.TotalPrizeFund.Should().Be(75m);
    }

    [Fact]
    public async Task Handle_ShouldReportNoPrizeFund_ForAFreeSeasonWithNoTopUps()
    {
        // Arrange
        Given(Data(seasons: [Season(SeasonId, Now.AddMonths(-1), Now.AddMonths(9))],
            leagues: [League(1, price: 0m, memberCount: 12)]));

        // Act
        var season = (await HandleAsync()).Single();

        // Assert
        season.TotalPrizeFund.Should().Be(0m);
    }

    #endregion

    private void Given(HomepageSeasonsData data) =>
        _query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(data);

    private static HomepageSeasonsData Data(
        params HomepageSeasonRow[] seasons) =>
        new(seasons, [], []);

    private static HomepageSeasonsData Data(
        HomepageSeasonRow[] seasons,
        HomepageLeagueRow[]? leagues = null,
        HomepageMembershipRow[]? memberships = null) =>
        new(seasons, leagues ?? [], memberships ?? []);

    private static HomepageSeasonRow Season(int id, DateTime startDateUtc, DateTime endDateUtc) =>
        new(id, id == SeasonId ? "2026/27" : $"Season {id}", CompetitionType.League, startDateUtc, endDateUtc);

    private static HomepageLeagueRow League(
        int leagueId,
        decimal price,
        int memberCount,
        decimal? prizeFundOverride = null,
        int seasonId = SeasonId) =>
        new(seasonId, leagueId, price, prizeFundOverride, memberCount);

    private Task<IEnumerable<HomepageSeasonDto>> HandleAsync() =>
        _handler.Handle(new GetHomepageSeasonsQuery(), CancellationToken.None);
}
