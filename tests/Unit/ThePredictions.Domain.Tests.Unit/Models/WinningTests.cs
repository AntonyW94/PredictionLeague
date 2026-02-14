using FluentAssertions;
using PredictionLeague.Domain.Models;
using ThePredictions.Domain.Tests.Unit.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class WinningTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateWinning_WhenValidParametersProvided()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 100m, 5, 3, _dateTimeProvider);

        // Assert
        winning.UserId.Should().Be("user-1");
        winning.LeaguePrizeSettingId.Should().Be(1);
        winning.Amount.Should().Be(100m);
        winning.RoundNumber.Should().Be(5);
        winning.Month.Should().Be(3);
    }

    [Fact]
    public void Create_ShouldSetAwardedDateUtc_WhenCreated()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 50m, null, null, _dateTimeProvider);

        // Assert
        winning.AwardedDateUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldAcceptNullRoundNumber()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 50m, null, 3, _dateTimeProvider);

        // Assert
        winning.RoundNumber.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldAcceptNullMonth()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 50m, 5, null, _dateTimeProvider);

        // Assert
        winning.Month.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetRoundNumber_WhenProvided()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 50m, 5, null, _dateTimeProvider);

        // Assert
        winning.RoundNumber.Should().Be(5);
    }

    [Fact]
    public void Create_ShouldSetMonth_WhenProvided()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 50m, null, 3, _dateTimeProvider);

        // Assert
        winning.Month.Should().Be(3);
    }

    [Fact]
    public void Create_ShouldAcceptBothRoundNumberAndMonth()
    {
        // Act
        var act = () => Winning.Create("user-1", 1, 50m, 5, 3, _dateTimeProvider);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_ShouldAllowZeroAmount()
    {
        // Act
        var winning = Winning.Create("user-1", 1, 0m, null, null, _dateTimeProvider);

        // Assert
        winning.Amount.Should().Be(0m);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => Winning.Create(null!, 1, 50m, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => Winning.Create("", 1, 50m, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => Winning.Create(" ", 1, 50m, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsZero()
    {
        // Act
        var act = () => Winning.Create("user-1", 0, 50m, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsNegative()
    {
        // Act
        var act = () => Winning.Create("user-1", -1, 50m, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAmountIsNegative()
    {
        // Act
        var act = () => Winning.Create("user-1", 1, -1m, null, null, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
