using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Contracts.Badges;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges.Queries;

/// <summary>
/// The dashboard badges tile, and in particular its "3rd of 44 for badges" line - which now comes from the same
/// standings the badges page shows, so the two cannot disagree about the same player.
/// </summary>
public class GetBadgesTileQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBadgeStateQuery _badgeStateQuery = Substitute.For<IBadgeStateQuery>();
    private readonly IBadgeLeaderboardQuery _badgeLeaderboardQuery = Substitute.For<IBadgeLeaderboardQuery>();
    private readonly GetBadgesTileQueryHandler _handler;

    public GetBadgesTileQueryHandlerTests()
    {
        _handler = new GetBadgesTileQueryHandler(_badgeStateQuery, _badgeLeaderboardQuery, new TestDateTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldReportTheirPositionAndHowManyPlayersThereAre()
    {
        // Arrange - two players, and this one has the fewer badges.
        GivenState();
        GivenLeaderboard(
            [Player(UserId, "Ada", "Lovelace"), Player("user-other", "Grace", "Hopper")],
            [Award("user-other", "banked")]);

        // Act
        var tile = await HandleAsync();

        // Assert
        tile.YourRank.Should().Be(2);
        tile.TotalPlayers.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldGiveThemThePositionTheyShareWithOthers()
    {
        // The whole reason the tile and the page now read from one place: three players holding nothing are joint
        // second, not second, third and fourth in whatever order their names happen to fall.
        GivenState();
        GivenLeaderboard(
            [
                Player(UserId, "Zara", "Zeta"),
                Player("user-a", "Ada", "Lovelace"),
                Player("user-b", "Grace", "Hopper"),
                Player("user-winner", "Alan", "Turing")
            ],
            [Award("user-winner", "banked")]);

        // Act
        var tile = await HandleAsync();

        // Assert
        tile.YourRank.Should().Be(2);
        tile.TotalPlayers.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldNotShowAPosition_ForAnAccountThatIsNotOnTheTable()
    {
        // An account with no name yet is not a player. The old statement handed it first place for having nobody
        // ahead of it, which the tile then announced.
        GivenState();
        GivenLeaderboard([Player(UserId, null, null), Player("user-other", "Grace", "Hopper")], []);

        // Act
        var tile = await HandleAsync();

        // Assert
        tile.YourRank.Should().BeNull();
        tile.TotalPlayers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldStillBuildTheCarousel_WhenNobodyHasEarnedAnything()
    {
        // Arrange
        GivenState();
        GivenLeaderboard([Player(UserId, "Ada", "Lovelace")], []);

        // Act
        var tile = await HandleAsync();

        // Assert
        tile.EarnedCount.Should().Be(0);
        tile.Carousel.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportTheBadgesTheyHold()
    {
        // Arrange
        GivenState(new BadgeStateData("Ada", "Lovelace", [new BadgeAwardRow("founder", Now.AddDays(-2))], [], 0));
        GivenLeaderboard([Player(UserId, "Ada", "Lovelace")], [Award(UserId, "founder")]);

        // Act
        var tile = await HandleAsync();

        // Assert
        tile.EarnedCount.Should().Be(1);
        tile.YourRank.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldAskForTheStateOfThePlayerRequested()
    {
        // Arrange
        GivenState();
        GivenLeaderboard([], []);

        // Act
        await HandleAsync();

        // Assert
        await _badgeStateQuery.Received(1).ExecuteAsync(UserId, Arg.Any<CancellationToken>());
    }

    private void GivenState(BadgeStateData? data = null) =>
        _badgeStateQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(data ?? new BadgeStateData("Ada", "Lovelace", [], [], 0));

    private void GivenLeaderboard(BadgePlayerRow[] players, BadgePlayerAwardRow[] awards) =>
        _badgeLeaderboardQuery.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(new BadgeLeaderboardData(players, awards));

    private static BadgePlayerRow Player(string userId, string? firstName, string? lastName) =>
        new(userId, firstName, lastName);

    private static BadgePlayerAwardRow Award(string userId, string badgeKey) =>
        new(userId, badgeKey, Now.AddDays(-5));

    private Task<BadgesTileDto> HandleAsync() =>
        _handler.Handle(new GetBadgesTileQuery(UserId), CancellationToken.None);
}
