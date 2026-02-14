using FluentAssertions;
using PredictionLeague.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class PasswordResetTokenTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateToken_WhenValidParametersProvided()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Assert
        token.Token.Should().Be("test-token-abc");
        token.UserId.Should().Be("user-1");
        token.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        token.ExpiresAtUtc.Should().Be(_dateTimeProvider.UtcNow.AddHours(1));
    }

    [Fact]
    public void Create_ShouldSetToken_WhenProvided()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-123", "user-1", _dateTimeProvider);

        // Assert
        token.Token.Should().Be("test-token-123");
    }

    [Fact]
    public void Create_ShouldSetUserId_WhenCreated()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Assert
        token.UserId.Should().Be("user-1");
    }

    [Fact]
    public void Create_ShouldSetCreatedAtUtc_WhenCreated()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Assert
        token.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldSetExpiryToOneHour_WhenDefaultExpiryUsed()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Assert
        token.ExpiresAtUtc.Should().Be(_dateTimeProvider.UtcNow.AddHours(1));
    }

    [Fact]
    public void Create_ShouldSetCustomExpiry_WhenExpiryHoursProvided()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider, expiryHours: 24);

        // Assert
        token.ExpiresAtUtc.Should().Be(_dateTimeProvider.UtcNow.AddHours(24));
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenTokenIsNull()
    {
        // Act
        var act = () => PasswordResetToken.Create(null!, "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenTokenIsEmpty()
    {
        // Act
        var act = () => PasswordResetToken.Create("", "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenTokenIsWhitespace()
    {
        // Act
        var act = () => PasswordResetToken.Create(" ", "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => PasswordResetToken.Create("test-token-abc", null!, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => PasswordResetToken.Create("test-token-abc", "", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => PasswordResetToken.Create("test-token-abc", " ", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region IsExpired

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenExpiryIsInFuture()
    {
        // Arrange
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Assert — time hasn't advanced past expiry
        token.IsExpired(_dateTimeProvider).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenExpiryIsInPast()
    {
        // Arrange
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Advance time past expiry
        _dateTimeProvider.UtcNow = token.ExpiresAtUtc.AddSeconds(1);

        // Assert
        token.IsExpired(_dateTimeProvider).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenExactlyAtExpiry()
    {
        // Arrange
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Advance time to exactly the expiry moment
        _dateTimeProvider.UtcNow = token.ExpiresAtUtc;

        // Assert — uses > (strictly greater), so equal is NOT expired
        token.IsExpired(_dateTimeProvider).Should().BeFalse();
    }

    #endregion
}
