using FluentAssertions;
using PredictionLeague.Domain.Models;
using ThePredictions.Domain.Tests.Unit.Helpers;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class RefreshTokenTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    private RefreshToken CreateActiveToken()
    {
        return new RefreshToken
        {
            UserId = "user-1",
            Token = "test-token",
            Created = _dateTimeProvider.UtcNow,
            Expires = _dateTimeProvider.UtcNow.AddDays(7)
        };
    }

    #region Revoke

    [Fact]
    public void Revoke_ShouldSetRevokedTimestamp_WhenCalled()
    {
        // Arrange
        var token = CreateActiveToken();

        // Act
        token.Revoke(_dateTimeProvider);

        // Assert
        token.Revoked.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Revoke_ShouldMakeTokenInactive_WhenTokenWasActive()
    {
        // Arrange
        var token = CreateActiveToken();

        // Act
        token.Revoke(_dateTimeProvider);

        // Assert
        token.IsActive(_dateTimeProvider).Should().BeFalse();
    }

    #endregion

    #region IsExpired

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenExpiresInFuture()
    {
        // Arrange
        var token = CreateActiveToken();

        // Assert
        token.IsExpired(_dateTimeProvider).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenExpiresInPast()
    {
        // Arrange
        var token = CreateActiveToken();

        // Advance past expiry
        _dateTimeProvider.UtcNow = token.Expires.AddSeconds(1);

        // Assert
        token.IsExpired(_dateTimeProvider).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenExactlyAtExpiry()
    {
        // Arrange
        var token = CreateActiveToken();

        // Advance to exactly the expiry moment
        _dateTimeProvider.UtcNow = token.Expires;

        // Assert — uses >= so equal IS expired (different from PasswordResetToken)
        token.IsExpired(_dateTimeProvider).Should().BeTrue();
    }

    #endregion

    #region IsActive

    [Fact]
    public void IsActive_ShouldReturnTrue_WhenNotRevokedAndNotExpired()
    {
        // Arrange
        var token = CreateActiveToken();

        // Assert
        token.IsActive(_dateTimeProvider).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenRevoked()
    {
        // Arrange
        var token = CreateActiveToken();
        token.Revoke(_dateTimeProvider);

        // Assert
        token.IsActive(_dateTimeProvider).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenExpired()
    {
        // Arrange
        var token = CreateActiveToken();
        _dateTimeProvider.UtcNow = token.Expires.AddSeconds(1);

        // Assert
        token.IsActive(_dateTimeProvider).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenBothRevokedAndExpired()
    {
        // Arrange
        var token = CreateActiveToken();
        token.Revoke(_dateTimeProvider);
        _dateTimeProvider.UtcNow = token.Expires.AddSeconds(1);

        // Assert
        token.IsActive(_dateTimeProvider).Should().BeFalse();
    }

    #endregion
}
