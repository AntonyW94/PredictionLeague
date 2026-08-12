using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Contracts.Leagues;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The Manage Leagues page. The read returns every league on the site, and the handler decides which an ordinary player is
/// allowed to be shown: their own private leagues and nothing else. Public and other people's private leagues are a site
/// administrator's view. That filter is the whole point of the handler, and a read that returns rows the caller must not see
/// makes it worth asserting rather than assuming.
/// </summary>
/// <remarks>
/// What used to arrive as a category tag computed by a <c>CASE</c> is now worked out here from the two facts it was derived
/// from - whether the league has an entry code, and who administers it.
/// </remarks>
public class GetManageLeaguesQueryHandlerTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IManageLeaguesQuery _manageLeaguesQuery = Substitute.For<IManageLeaguesQuery>();
    private readonly GetManageLeaguesQueryHandler _handler;

    public GetManageLeaguesQueryHandlerTests()
    {
        _handler = new GetManageLeaguesQueryHandler(_manageLeaguesQuery);
    }

    #region What an ordinary player may see

    [Fact]
    public async Task Handle_ShouldReturnOnlyTheirOwnPrivateLeagues_ForAnOrdinaryPlayer()
    {
        // Arrange
        GivenLeagues(PublicLeague(1), MyPrivateLeague(2), OtherPrivateLeague(3));

        // Act
        var result = await HandleAsync(isAdmin: false);

        // Assert
        result.MyPrivateLeagues.Select(league => league.Id).Should().Equal(2);
        result.PublicLeagues.Should().BeEmpty();
        result.OtherPrivateLeagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotLeakOtherPeoplesPrivateLeagues_EvenThoughTheReadReturnsThem()
    {
        // The read is deliberately unfiltered, so this is the only thing standing between one player and everybody else's
        // private leagues.
        GivenLeagues(OtherPrivateLeague(3));

        // Act
        var result = await HandleAsync(isAdmin: false);

        // Assert
        result.MyPrivateLeagues.Should().BeEmpty();
        result.OtherPrivateLeagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryCategory_ForAnAdministrator()
    {
        // Arrange
        GivenLeagues(PublicLeague(1), MyPrivateLeague(2), OtherPrivateLeague(3));

        // Act
        var result = await HandleAsync(isAdmin: true);

        // Assert
        result.PublicLeagues.Select(league => league.Id).Should().Equal(1);
        result.MyPrivateLeagues.Select(league => league.Id).Should().Equal(2);
        result.OtherPrivateLeagues.Select(league => league.Id).Should().Equal(3);
    }

    #endregion

    #region Sorting the leagues into their categories

    [Fact]
    public async Task Handle_ShouldTreatALeagueWithNoEntryCodeAsPublic()
    {
        // No code means anybody can join, which is the fact the old statement turned into the word "Public" twice over - once
        // as a category and once in place of the code itself.
        GivenLeagues(MyPrivateLeague(1) with { EntryCode = null });

        // Act
        var result = await HandleAsync(isAdmin: true);

        // Assert
        result.PublicLeagues.Select(league => league.Id).Should().Equal(1);
        result.MyPrivateLeagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSortAPrivateLeagueByWhoAdministersIt()
    {
        // Arrange
        GivenLeagues(MyPrivateLeague(1), OtherPrivateLeague(2));

        // Act
        var result = await HandleAsync(isAdmin: true);

        // Assert
        result.MyPrivateLeagues.Select(league => league.Id).Should().Equal(1);
        result.OtherPrivateLeagues.Select(league => league.Id).Should().Equal(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyLists_WhenThereAreNoLeagues()
    {
        // Arrange
        GivenLeagues();

        // Act
        var result = await HandleAsync(isAdmin: true);

        // Assert
        result.PublicLeagues.Should().BeEmpty();
        result.MyPrivateLeagues.Should().BeEmpty();
        result.OtherPrivateLeagues.Should().BeEmpty();
    }

    #endregion

    #region Order and contents

    [Fact]
    public async Task Handle_ShouldListTheNewestSeasonFirst()
    {
        // By the season's start date rather than its name, so a season does not depend on how it happens to be named.
        GivenLeagues(
            PublicLeague(1) with { SeasonStartDateUtc = SeasonStart.AddYears(-1), SeasonName = "2025/26" },
            PublicLeague(2) with { SeasonStartDateUtc = SeasonStart, SeasonName = "2026/27" });

        // Act
        var result = await HandleAsync(isAdmin: true);

        // Assert
        result.PublicLeagues.Select(league => league.Id).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldListLeaguesAlphabeticallyWithinASeason()
    {
        // Arrange - the ids run the other way to the names.
        GivenLeagues(PublicLeague(1) with { Name = "Zulu" }, PublicLeague(2) with { Name = "Alpha" });

        // Act
        var result = await HandleAsync(isAdmin: true);

        // Assert
        result.PublicLeagues.Select(league => league.Name).Should().Equal("Alpha", "Zulu");
    }

    [Fact]
    public async Task Handle_ShouldCarryEveryLeagueFieldThrough()
    {
        // Arrange
        GivenLeagues(MyPrivateLeague(7) with
        {
            Name = "Alpha League",
            SeasonName = "2026/27",
            MemberCount = 8,
            Price = 10m,
            EntryCode = "ABC123",
            EntryDeadlineUtc = Deadline,
            PointsForExactScore = 3,
            PointsForCorrectResult = 1
        });

        // Act
        var league = (await HandleAsync(isAdmin: false)).MyPrivateLeagues.Single();

        // Assert
        league.Id.Should().Be(7);
        league.Name.Should().Be("Alpha League");
        league.SeasonName.Should().Be("2026/27");
        league.MemberCount.Should().Be(8);
        league.Price.Should().Be(10m);
        league.EntryCode.Should().Be("ABC123");
        league.EntryDeadlineUtc.Should().Be(Deadline);
        league.PointsForExactScore.Should().Be(3);
        league.PointsForCorrectResult.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldShowPublicWhereAPublicLeaguesEntryCodeWouldBe()
    {
        // A label, not a code. The statement this replaces produced the word with an ISNULL, so a sentinel travelled to the
        // browser in a field named for a code.
        GivenLeagues(PublicLeague(1));

        // Act
        var league = (await HandleAsync(isAdmin: true)).PublicLeagues.Single();

        // Assert
        league.EntryCode.Should().Be("Public");
    }

    [Fact]
    public async Task Handle_ShouldReportALeagueWithNoEntryDeadline()
    {
        // The column allows it, and the old statement read it into a field that said it could not - which would have thrown
        // rather than rendered.
        GivenLeagues(MyPrivateLeague(1) with { EntryDeadlineUtc = null });

        // Act
        var league = (await HandleAsync(isAdmin: false)).MyPrivateLeagues.Single();

        // Assert
        league.EntryDeadlineUtc.Should().BeNull();
    }

    #endregion

    private void GivenLeagues(params ManageLeagueRow[] leagues) =>
        _manageLeaguesQuery.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(leagues);

    private static ManageLeagueRow PublicLeague(int id) => Row(id, entryCode: null, administratorUserId: OtherUserId);

    private static ManageLeagueRow MyPrivateLeague(int id) => Row(id, entryCode: "ABC123", administratorUserId: UserId);

    private static ManageLeagueRow OtherPrivateLeague(int id) => Row(id, entryCode: "XYZ789", administratorUserId: OtherUserId);

    private static ManageLeagueRow Row(int id, string? entryCode, string administratorUserId) =>
        new(id, $"League {id}", SeasonId: 7, "2026/27", SeasonStart, administratorUserId,
            MemberCount: 8, Price: 10m, entryCode, Deadline, PointsForExactScore: 3, PointsForCorrectResult: 1);

    private Task<ManageLeaguesDto> HandleAsync(bool isAdmin) =>
        _handler.Handle(new GetManageLeaguesQuery(UserId, isAdmin), CancellationToken.None);
}
