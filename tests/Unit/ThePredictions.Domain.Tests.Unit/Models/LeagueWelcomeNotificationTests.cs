using FluentAssertions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeagueWelcomeNotificationTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateNotification_WhenValidParametersProvided()
    {
        // Act
        var notification = LeagueWelcomeNotification.Create(1, "user-1", _dateTimeProvider);

        // Assert
        notification.LeagueId.Should().Be(1);
        notification.UserId.Should().Be("user-1");
    }

    [Fact]
    public void Create_ShouldSetSentAtUtc_WhenCreated()
    {
        // Act
        var notification = LeagueWelcomeNotification.Create(1, "user-1", _dateTimeProvider);

        // Assert
        notification.SentAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenLeagueIdIsZero()
    {
        // Act
        var act = () => LeagueWelcomeNotification.Create(0, "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeagueIdIsNegative()
    {
        // Act
        var act = () => LeagueWelcomeNotification.Create(-1, "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => LeagueWelcomeNotification.Create(1, null!, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => LeagueWelcomeNotification.Create(1, "", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => LeagueWelcomeNotification.Create(1, " ", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
