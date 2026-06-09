using FluentAssertions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class PrizeNotificationTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreatePrizeNotification_WhenValidParametersProvided()
    {
        // Act
        var notification = PrizeNotification.Create("user-1", 1, 5, 3, _dateTimeProvider);

        // Assert
        notification.UserId.Should().Be("user-1");
        notification.LeaguePrizeSettingId.Should().Be(1);
        notification.RoundNumber.Should().Be(5);
        notification.Month.Should().Be(3);
    }

    [Fact]
    public void Create_ShouldSetSentAtUtc_WhenCreated()
    {
        // Act
        var notification = PrizeNotification.Create("user-1", 1, null, null, _dateTimeProvider);

        // Assert
        notification.SentAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldAcceptNullRoundNumber()
    {
        // Act
        var notification = PrizeNotification.Create("user-1", 1, null, 3, _dateTimeProvider);

        // Assert
        notification.RoundNumber.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldAcceptNullMonth()
    {
        // Act
        var notification = PrizeNotification.Create("user-1", 1, 5, null, _dateTimeProvider);

        // Assert
        notification.Month.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldAcceptBothRoundNumberAndMonthNull()
    {
        // Act
        var act = () => PrizeNotification.Create("user-1", 1, null, null, _dateTimeProvider);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => PrizeNotification.Create(null!, 1, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => PrizeNotification.Create("", 1, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => PrizeNotification.Create(" ", 1, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsZero()
    {
        // Act
        var act = () => PrizeNotification.Create("user-1", 0, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsNegative()
    {
        // Act
        var act = () => PrizeNotification.Create("user-1", -1, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
