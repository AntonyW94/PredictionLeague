using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Contracts.Badges;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges.Queries;

/// <summary>The site-wide badges table, with the viewer's own row marked so the page can highlight it.</summary>
public class GetBadgeLeaderboardQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime AwardedUtc = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IBadgeLeaderboardQuery _badgeLeaderboardQuery = Substitute.For<IBadgeLeaderboardQuery>();
    private readonly GetBadgeLeaderboardQueryHandler _handler;

    public GetBadgeLeaderboardQueryHandlerTests()
    {
        _handler = new GetBadgeLeaderboardQueryHandler(_badgeLeaderboardQuery);
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyTable_WhenThereAreNoPlayers()
    {
        // Arrange
        Given([], []);

        // Act
        var leaderboard = await HandleAsync();

        // Assert
        leaderboard.Rows.Should().BeEmpty();
        leaderboard.TotalPlayers.Should().Be(0);
        leaderboard.YourRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldRankEveryPlayerAndCountThem()
    {
        // Arrange
        Given(
            [Player(UserId, "Ada", "Lovelace"), Player("user-other", "Grace", "Hopper")],
            [Award("user-other", "banked"), Award("user-other", "founder"), Award(UserId, "banked")]);

        // Act
        var leaderboard = await HandleAsync();

        // Assert
        leaderboard.TotalPlayers.Should().Be(2);
        leaderboard.Rows.Select(row => row.Rank).Should().Equal(1, 2);
        leaderboard.Rows.Select(row => row.UserId).Should().Equal("user-other", UserId);
    }

    [Fact]
    public async Task Handle_ShouldMarkTheViewersOwnRowAndReportTheirPosition()
    {
        // Arrange
        Given(
            [Player(UserId, "Ada", "Lovelace"), Player("user-other", "Grace", "Hopper")],
            [Award("user-other", "banked")]);

        // Act
        var leaderboard = await HandleAsync();

        // Assert
        leaderboard.YourRank.Should().Be(2);
        leaderboard.Rows.Single(row => row.IsCurrentUser).UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task Handle_ShouldNotReportAPosition_WhenTheViewerIsNotOnTheTable()
    {
        // An administrator looking at the table without a completed profile of their own.
        Given([Player("user-other", "Grace", "Hopper")], []);

        // Act
        var leaderboard = await HandleAsync();

        // Assert
        leaderboard.YourRank.Should().BeNull();
        leaderboard.Rows.Should().NotContain(row => row.IsCurrentUser);
    }

    [Fact]
    public async Task Handle_ShouldReportEachPlayersNameTallyAndLastAward()
    {
        // Arrange
        Given([Player(UserId, "Ada", "Lovelace")], [Award(UserId, "banked"), Award(UserId, "founder")]);

        // Act
        var leaderboard = await HandleAsync();

        // Assert
        var row = leaderboard.Rows.Single();
        row.DisplayName.Should().Be("Ada L");
        row.BadgeCount.Should().Be(2);
        row.LastAwardedUtc.Should().Be(AwardedUtc);
        row.TotalBadges.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task Handle_ShouldReportNoLastAward_ForAPlayerWithNoBadges()
    {
        // Arrange
        Given([Player(UserId, "Ada", "Lovelace")], []);

        // Act
        var leaderboard = await HandleAsync();

        // Assert
        leaderboard.Rows.Single().LastAwardedUtc.Should().BeNull();
        leaderboard.Rows.Single().BadgeCount.Should().Be(0);
    }

    private void Given(BadgePlayerRow[] players, BadgePlayerAwardRow[] awards) =>
        _badgeLeaderboardQuery.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(new BadgeLeaderboardData(players, awards));

    private static BadgePlayerRow Player(string userId, string? firstName, string? lastName) =>
        new(userId, firstName, lastName);

    private static BadgePlayerAwardRow Award(string userId, string badgeKey) =>
        new(userId, badgeKey, AwardedUtc);

    private Task<BadgeLeaderboardDto> HandleAsync() =>
        _handler.Handle(new GetBadgeLeaderboardQuery(UserId), CancellationToken.None);
}
