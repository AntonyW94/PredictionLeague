using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The leagues a player is offered on the dashboard.
///
/// Three rules decide whether a league appears, and all three were <c>WHERE</c> clauses - one of them reading the database's
/// own clock.
/// </summary>
public class GetAvailableLeaguesQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IJoinableLeaguesQuery _joinableLeaguesQuery = Substitute.For<IJoinableLeaguesQuery>();
    private readonly GetAvailableLeaguesQueryHandler _handler;

    public GetAvailableLeaguesQueryHandlerTests()
    {
        _handler = new GetAvailableLeaguesQueryHandler(_joinableLeaguesQuery, new TestDateTimeProvider(Now));
    }

    #region Which leagues are offered

    [Fact]
    public async Task Handle_ShouldOfferAPublicLeagueThatIsStillOpen()
    {
        // Arrange
        Given(League(1, "Open League"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Select(league => league.Name).Should().Equal("Open League");
    }

    [Fact]
    public async Task Handle_ShouldNotOfferALeagueWhoseDeadlineHasPassed()
    {
        // Arrange
        Given(League(1, "Closed", entryDeadlineUtc: Now.AddHours(-1)));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotOfferALeagueWithNoDeadlineAtAll()
    {
        // Arrange - in SQL this fell out of NULL > GETUTCDATE() being unknown, so it was never written down. Stated with
        // "with" rather than by passing null, because the helper reads a null argument as "unspecified".
        Given(League(1, "No Deadline") with { EntryDeadlineUtc = null });

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotOfferAnUnlistedPrivateLeague()
    {
        // Arrange
        Given(League(1, "Secret", hasEntryCode: true, isListed: false));

        // Act
        var leagues = await HandleAsync();

        // Assert - the point of a private league is that you have to be told about it.
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOfferAPrivateLeagueItsAdministratorHasListed()
    {
        // Arrange
        Given(League(1, "Listed Private", hasEntryCode: true, isListed: true));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Single().IsPrivate.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldStillOfferALeagueInASeasonThePlayerHasNoPassFor()
    {
        // Hiding these read as "there is nothing to join", which is what confused people: a pass is bought per season, and
        // somebody without one saw an empty list rather than a reason to buy one.
        Given(League(1, "Needs A Pass", hasSeasonPass: false));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.Name.Should().Be("Needs A Pass");
        league.RequiresSeasonPass.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotAskForAPass_WhenThePlayerAlreadyHoldsOne()
    {
        // Arrange
        Given(League(1, "Already Bought In", hasSeasonPass: true));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert - the flag decides what the button says, so it has to run the right way round.
        league.RequiresSeasonPass.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldOfferNothing_WhenThereAreNoJoinableLeagues()
    {
        // Arrange
        Given();

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    #endregion

    #region What each offer shows

    [Fact]
    public async Task Handle_ShouldWorkOutTheEstimatedPotFromTheEntryFeesAndTheTopUp()
    {
        // Arrange
        Given(League(1, "Big Money", price: 10m, memberCount: 12, prizeFundOverride: 50m));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert - the same pot rule as everywhere else that adds the administrator's top-up.
        league.EstPot.Should().Be(170m);
        league.MemberCount.Should().Be(12);
        league.Price.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_ShouldMarkAPublicLeagueAsNotPrivate()
    {
        // Arrange
        Given(League(1, "Public", hasEntryCode: false));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheSeasonNameAndDeadline()
    {
        // Arrange
        var deadline = Now.AddDays(3);
        Given(League(1, "Open", entryDeadlineUtc: deadline));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.SeasonName.Should().Be("2026/27");
        league.EntryDeadlineUtc.Should().Be(deadline);
    }

    #endregion

    #region The order they appear in

    [Fact]
    public async Task Handle_ShouldOfferTheNewestSeasonFirst()
    {
        // Arrange
        Given(
            League(1, "Older", seasonStartDateUtc: SeasonStart),
            League(2, "Newer", seasonStartDateUtc: SeasonStart.AddYears(1)));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Select(league => league.Name).Should().Equal("Newer", "Older");
    }

    [Fact]
    public async Task Handle_ShouldThenOrderByName()
    {
        // Arrange
        Given(League(1, "Zebras"), League(2, "Aardvarks"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Select(league => league.Name).Should().Equal("Aardvarks", "Zebras");
    }

    #endregion

    private void Given(params JoinableLeagueRow[] leagues)
    {
        _joinableLeaguesQuery.ExecuteAsync(UserId, Arg.Any<CancellationToken>()).Returns(leagues);
    }

    private async Task<IEnumerable<AvailableLeagueDto>> HandleAsync() =>
        await _handler.Handle(new GetAvailableLeaguesQuery(UserId), CancellationToken.None);

    /// <summary>
    /// A league that is on offer unless a test says otherwise. A null <paramref name="entryDeadlineUtc"/> means
    /// "unspecified", not "no deadline" - for the latter, use <c>League(...) with { EntryDeadlineUtc = null }</c>, which says
    /// so plainly. An earlier version of this helper made the two indistinguishable and the null case untestable.
    /// </summary>
    private static JoinableLeagueRow League(
        int leagueId,
        string name,
        decimal price = 10m,
        decimal? prizeFundOverride = null,
        DateTime? entryDeadlineUtc = null,
        bool hasEntryCode = false,
        bool isListed = false,
        int memberCount = 5,
        bool hasSeasonPass = true,
        DateTime? seasonStartDateUtc = null) =>
        new(
            leagueId,
            name,
            "2026/27",
            seasonStartDateUtc ?? SeasonStart,
            price,
            prizeFundOverride,
            entryDeadlineUtc ?? Now.AddDays(7),
            hasEntryCode,
            isListed,
            memberCount,
            hasSeasonPass);
}
