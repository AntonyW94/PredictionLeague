using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Contracts.Leagues;
using Xunit;
using static ThePredictions.Application.Features.Leagues.Queries.GetManageLeaguesQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The Manage Leagues page. The SQL returns every league the caller could possibly see, tagged with a
/// category, and the handler decides which of those tags an ordinary player is allowed to be shown:
/// their own private leagues and nothing else. Public and other people's private leagues are a site
/// administrator's view. That filter is the whole point of the handler, and a query returning rows the
/// caller must not see makes it worth asserting rather than assuming.
/// </summary>
public class GetManageLeaguesQueryHandlerTests
{
    private const string UserId = "user-1";

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly GetManageLeaguesQueryHandler _handler;

    public GetManageLeaguesQueryHandlerTests()
    {
        _handler = new GetManageLeaguesQueryHandler(_dbConnection);
    }

    private static LeagueWithCategory Row(string category, int id = 1, string name = "Test League") =>
        new(id, name, "2026/27", 8, 10m, "ABC123", new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc), 3, 1, category);

    private void GivenLeagues(params LeagueWithCategory[] rows) =>
        _dbConnection.QueryAsync<LeagueWithCategory>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(rows);

    private Task<ManageLeaguesDto> HandleAsync(bool isAdmin) =>
        _handler.Handle(new GetManageLeaguesQuery(UserId, isAdmin), CancellationToken.None);

    // ---------- what an ordinary player may see ----------

    [Fact]
    public async Task Handle_ShouldReturnOnlyTheirOwnPrivateLeagues_ForAnOrdinaryPlayer()
    {
        GivenLeagues(Row("Public", 1), Row("MyPrivate", 2), Row("OtherPrivate", 3));

        var result = await HandleAsync(isAdmin: false);

        result.MyPrivateLeagues.Should().ContainSingle().Which.Id.Should().Be(2);
        result.PublicLeagues.Should().BeEmpty();
        result.OtherPrivateLeagues.Should().BeEmpty();
    }

    // The rows are in the result set either way - the SQL does not filter them - so dropping this
    // branch would expose other people's private leagues to every player.
    [Fact]
    public async Task Handle_ShouldNotLeakOtherPeoplesPrivateLeagues_EvenThoughTheQueryReturnsThem()
    {
        GivenLeagues(Row("OtherPrivate", 3, "Someone Else's League"));

        var result = await HandleAsync(isAdmin: false);

        result.OtherPrivateLeagues.Should().BeEmpty();
        result.MyPrivateLeagues.Should().BeEmpty();
        result.PublicLeagues.Should().BeEmpty();
    }

    // ---------- what an administrator may see ----------

    [Fact]
    public async Task Handle_ShouldReturnEveryCategory_ForAnAdministrator()
    {
        GivenLeagues(Row("Public", 1), Row("MyPrivate", 2), Row("OtherPrivate", 3));

        var result = await HandleAsync(isAdmin: true);

        result.PublicLeagues.Should().ContainSingle().Which.Id.Should().Be(1);
        result.MyPrivateLeagues.Should().ContainSingle().Which.Id.Should().Be(2);
        result.OtherPrivateLeagues.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldGroupSeveralLeaguesIntoTheirOwnCategories()
    {
        GivenLeagues(
            Row("Public", 1), Row("Public", 2),
            Row("MyPrivate", 3),
            Row("OtherPrivate", 4), Row("OtherPrivate", 5), Row("OtherPrivate", 6));

        var result = await HandleAsync(isAdmin: true);

        result.PublicLeagues.Should().HaveCount(2);
        result.MyPrivateLeagues.Should().HaveCount(1);
        result.OtherPrivateLeagues.Should().HaveCount(3);
    }

    // An unrecognised tag is dropped rather than defaulting into a visible list.
    [Fact]
    public async Task Handle_ShouldIgnoreARowWithAnUnknownCategory()
    {
        GivenLeagues(Row("Something Else", 9));

        var result = await HandleAsync(isAdmin: true);

        result.PublicLeagues.Should().BeEmpty();
        result.MyPrivateLeagues.Should().BeEmpty();
        result.OtherPrivateLeagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyLists_WhenThereAreNoLeagues()
    {
        GivenLeagues();

        var result = await HandleAsync(isAdmin: true);

        result.PublicLeagues.Should().BeEmpty();
        result.MyPrivateLeagues.Should().BeEmpty();
        result.OtherPrivateLeagues.Should().BeEmpty();
    }

    // ---------- mapping ----------

    [Fact]
    public async Task Handle_ShouldCarryEveryLeagueFieldThrough()
    {
        var deadline = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        GivenLeagues(new LeagueWithCategory(7, "The Office", "2026/27", 12, 15.50m, "XYZ789", deadline, 5, 2, "MyPrivate"));

        var league = (await HandleAsync(isAdmin: false)).MyPrivateLeagues.Single();

        league.Id.Should().Be(7);
        league.Name.Should().Be("The Office");
        league.SeasonName.Should().Be("2026/27");
        league.MemberCount.Should().Be(12);
        league.Price.Should().Be(15.50m);
        league.EntryCode.Should().Be("XYZ789");
        league.EntryDeadlineUtc.Should().Be(deadline);
        league.PointsForExactScore.Should().Be(5);
        league.PointsForCorrectResult.Should().Be(2);
    }
}
