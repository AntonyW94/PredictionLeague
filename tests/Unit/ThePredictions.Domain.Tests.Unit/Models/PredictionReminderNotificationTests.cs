using FluentAssertions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class PredictionReminderNotificationTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateNotification_WhenValidParametersProvided()
    {
        // Act
        var notification = PredictionReminderNotification.Create(5, "user-1", "admin-1", _dateTimeProvider);

        // Assert
        notification.RoundId.Should().Be(5);
        notification.UserId.Should().Be("user-1");
        notification.RemindedByUserId.Should().Be("admin-1");
    }

    [Fact]
    public void Create_ShouldSetLastRemindedUtc_WhenCreated()
    {
        // Act
        var notification = PredictionReminderNotification.Create(5, "user-1", "admin-1", _dateTimeProvider);

        // Assert
        notification.LastRemindedUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenRoundIdIsZero()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(0, "user-1", "admin-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRoundIdIsNegative()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(-1, "user-1", "admin-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(5, null!, "admin-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(5, "", "admin-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(5, " ", "admin-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRemindedByUserIdIsNull()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(5, "user-1", null!, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRemindedByUserIdIsEmpty()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(5, "user-1", "", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRemindedByUserIdIsWhitespace()
    {
        // Act
        var act = () => PredictionReminderNotification.Create(5, "user-1", " ", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
