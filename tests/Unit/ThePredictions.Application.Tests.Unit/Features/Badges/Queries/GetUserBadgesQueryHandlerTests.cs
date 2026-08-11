using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges.Queries;

/// <summary>
/// The badges page. The handler's own job is small - whose page it is, and the name at the top of it - since the
/// catalogue builds the badges themselves.
/// </summary>
public class GetUserBadgesQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBadgeStateQuery _badgeStateQuery = Substitute.For<IBadgeStateQuery>();
    private readonly GetUserBadgesQueryHandler _handler;

    public GetUserBadgesQueryHandlerTests()
    {
        _handler = new GetUserBadgesQueryHandler(_badgeStateQuery, new TestDateTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldTitleThePageWithTheOwnersFirstNameAndLastInitial()
    {
        // Arrange - the page can be looked at for another player, so it says whose badges these are.
        Given(new BadgeStateData("Ada", "Lovelace", [], [], 0));

        // Act
        var page = await HandleAsync();

        // Assert
        page.OwnerName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheOwnerNameEmpty_WhenThereIsNoSuchPlayer()
    {
        // Arrange - an id that matches nobody. The page still renders rather than throwing.
        Given(new BadgeStateData(null, null, [], [], 0));

        // Act
        var page = await HandleAsync();

        // Assert
        page.OwnerName.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportTheBadgesTheyHold()
    {
        // Arrange
        Given(new BadgeStateData("Ada", "Lovelace", [new BadgeAwardRow("founder", Now.AddDays(-2))], [], 0));

        // Act
        var page = await HandleAsync();

        // Assert
        page.EarnedCount.Should().Be(1);
        page.TotalCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Handle_ShouldAskForTheBadgesOfThePlayerRequested()
    {
        // Arrange
        Given(new BadgeStateData("Ada", "Lovelace", [], [], 0));

        // Act
        await HandleAsync();

        // Assert
        await _badgeStateQuery.Received(1).ExecuteAsync(UserId, Arg.Any<CancellationToken>());
    }

    private void Given(BadgeStateData data) =>
        _badgeStateQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(data);

    private Task<Contracts.Badges.UserBadgesDto> HandleAsync() =>
        _handler.Handle(new GetUserBadgesQuery(UserId), CancellationToken.None);
}
