using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// Whether the dashboard offers somewhere to type an entry code.
///
/// A looser question than the available-leagues list asks, and the tests below are mostly about where the two differ.
/// </summary>
public class CheckForAvailablePrivateLeaguesQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IJoinableLeaguesQuery _joinableLeaguesQuery = Substitute.For<IJoinableLeaguesQuery>();
    private readonly CheckForAvailablePrivateLeaguesQueryHandler _handler;

    public CheckForAvailablePrivateLeaguesQueryHandlerTests()
    {
        _handler = new CheckForAvailablePrivateLeaguesQueryHandler(
            _joinableLeaguesQuery, new TestDateTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldSayYes_WhenAnOpenPrivateLeagueExists()
    {
        // Arrange
        Given(League(hasEntryCode: true));

        // Act
        var hasAny = await HandleAsync();

        // Assert
        hasAny.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayYes_ForAnUnlistedPrivateLeague()
    {
        // Arrange
        Given(League(hasEntryCode: true, isListed: false));

        // Act
        var hasAny = await HandleAsync();

        // Assert - unlike the available-leagues list, which hides these: somebody who has been given a code should be able
        // to use it.
        hasAny.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayYes_EvenWithoutASeasonPass()
    {
        // Arrange
        Given(League(hasEntryCode: true, hasSeasonPass: false));

        // Act
        var hasAny = await HandleAsync();

        // Assert - preserved from the old statement, which never checked. It means the prompt can appear for a league the
        // player cannot yet enter, and it is flagged in the plan document as looking more like an oversight than a decision.
        hasAny.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayNo_ForAPublicLeague()
    {
        // Arrange
        Given(League(hasEntryCode: false));

        // Act
        var hasAny = await HandleAsync();

        // Assert - a public league needs no code, so it is no reason to offer the box.
        hasAny.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSayNo_WhenThePrivateLeaguesDeadlineHasPassed()
    {
        // Arrange
        Given(League(hasEntryCode: true, entryDeadlineUtc: Now.AddHours(-1)));

        // Act
        var hasAny = await HandleAsync();

        // Assert
        hasAny.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSayNo_ForAPrivateLeagueWithNoDeadlineAtAll()
    {
        // Arrange
        Given(League(hasEntryCode: true) with { EntryDeadlineUtc = null });

        // Act
        var hasAny = await HandleAsync();

        // Assert
        hasAny.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSayNo_WhenThereIsNothingToJoin()
    {
        // Arrange
        Given();

        // Act
        var hasAny = await HandleAsync();

        // Assert
        hasAny.Should().BeFalse();
    }

    private void Given(params JoinableLeagueRow[] leagues)
    {
        _joinableLeaguesQuery.ExecuteAsync(UserId, Arg.Any<CancellationToken>()).Returns(leagues);
    }

    private async Task<bool> HandleAsync() =>
        await _handler.Handle(new CheckForAvailablePrivateLeaguesQuery(UserId), CancellationToken.None);

    /// <summary>
    /// A private-or-public league that is open unless a test says otherwise. A null <paramref name="entryDeadlineUtc"/>
    /// means "unspecified"; for a league with no deadline use <c>League(...) with { EntryDeadlineUtc = null }</c>.
    /// </summary>
    private static JoinableLeagueRow League(
        bool hasEntryCode,
        bool isListed = true,
        bool hasSeasonPass = true,
        DateTime? entryDeadlineUtc = null) =>
        new(
            1,
            "Test League",
            "2026/27",
            Now.AddMonths(-1),
            10m,
            null,
            entryDeadlineUtc ?? Now.AddDays(7),
            hasEntryCode,
            isListed,
            5,
            hasSeasonPass);
}
