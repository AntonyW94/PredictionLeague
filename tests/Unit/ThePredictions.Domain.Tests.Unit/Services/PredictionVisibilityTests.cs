using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// The secrecy rule the league dashboard's grid hides predictions by, which used to be a <c>CASE</c> over
/// <c>GETUTCDATE()</c> and so could not be reached from a test at all.
/// </summary>
public class PredictionVisibilityTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private const string Owner = "owner-user";
    private const string Viewer = "viewer-user";

    [Fact]
    public void IsVisibleTo_ShouldReturnTrue_WhenTheViewerOwnsThePrediction()
    {
        // Arrange - an hour before the deadline, so nobody else could see it.
        var match = MatchWith(customLockTimeUtc: null);

        // Act
        var isVisible = PredictionVisibility.IsVisibleTo(match, Owner, Owner, Now, Now.AddHours(1));

        // Assert
        isVisible.Should().BeTrue("a player always sees their own prediction.");
    }

    [Fact]
    public void IsVisibleTo_ShouldReturnFalse_WhenAnotherPlayersFixtureHasNotLocked()
    {
        // Arrange
        var match = MatchWith(customLockTimeUtc: null);

        // Act
        var isVisible = PredictionVisibility.IsVisibleTo(match, Owner, Viewer, Now, Now.AddHours(1));

        // Assert
        isVisible.Should().BeFalse("there is still time to copy it.");
    }

    [Fact]
    public void IsVisibleTo_ShouldReturnTrue_WhenAnotherPlayersFixtureHasLocked()
    {
        // Arrange
        var match = MatchWith(customLockTimeUtc: null);

        // Act
        var isVisible = PredictionVisibility.IsVisibleTo(match, Owner, Viewer, Now, Now.AddHours(-1));

        // Assert
        isVisible.Should().BeTrue();
    }

    [Fact]
    public void IsVisibleTo_ShouldReturnTrue_WhenTheDeadlineIsExactlyNow()
    {
        // Arrange - the boundary the old SQL's ">" put on this side, and Match.IsPredictionLocked keeps.
        var match = MatchWith(customLockTimeUtc: null);

        // Act
        var isVisible = PredictionVisibility.IsVisibleTo(match, Owner, Viewer, Now, Now);

        // Assert
        isVisible.Should().BeTrue("a fixture whose deadline is exactly now has locked.");
    }

    [Fact]
    public void IsVisibleTo_ShouldUseTheFixturesCustomLockTime_WhenItBringsTheDeadlineForward()
    {
        // Arrange - the round is open for another hour, but this fixture kicked off early.
        var match = MatchWith(customLockTimeUtc: Now.AddMinutes(-30));

        // Act
        var isVisible = PredictionVisibility.IsVisibleTo(match, Owner, Viewer, Now, Now.AddHours(1));

        // Assert
        isVisible.Should().BeTrue("the fixture's own lock time decides, not the round's deadline.");
    }

    [Fact]
    public void IsVisibleTo_ShouldUseTheFixturesCustomLockTime_WhenItPushesTheDeadlineBack()
    {
        // Arrange - the round's deadline has passed, but this fixture locks later.
        var match = MatchWith(customLockTimeUtc: Now.AddMinutes(30));

        // Act
        var isVisible = PredictionVisibility.IsVisibleTo(match, Owner, Viewer, Now, Now.AddHours(-1));

        // Assert
        isVisible.Should().BeFalse("a later lock time keeps the prediction hidden past the round's deadline.");
    }

    private static Match MatchWith(DateTime? customLockTimeUtc) =>
        new(
            id: 1,
            roundId: 1,
            homeTeamId: 10,
            awayTeamId: 20,
            matchDateTimeUtc: Now.AddHours(2),
            customLockTimeUtc: customLockTimeUtc,
            status: MatchStatus.Scheduled,
            actualHomeTeamScore: null,
            actualAwayTeamScore: null,
            externalId: null,
            matchNumber: 1,
            placeholderHomeName: null,
            placeholderAwayName: null,
            apiRoundName: null);
}
