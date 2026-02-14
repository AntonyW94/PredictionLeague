using FluentAssertions;
using PredictionLeague.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class RefreshTokenTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

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

    #region Constructor

    [Fact]
    public void Constructor_ShouldSetAllProperties_WhenCalledWithParameters()
    {
        // Arrange
        var created = _dateTimeProvider.UtcNow;
        var expires = created.AddDays(7);
        var revoked = created.AddDays(1);

        // Act
        var token = new RefreshToken(
            id: 5,
            userId: "user-1",
            token: "abc-token",
            expires: expires,
            created: created,
            revoked: revoked);

        // Assert
        token.Id.Should().Be(5);
        token.UserId.Should().Be("user-1");
        token.Token.Should().Be("abc-token");
        token.Expires.Should().Be(expires);
        token.Created.Should().Be(created);
        token.Revoked.Should().Be(revoked);
    }

    [Fact]
    public void Constructor_ShouldSetRevokedToNull_WhenNotRevoked()
    {
        // Act
        var token = new RefreshToken(
            id: 1,
            userId: "user-1",
            token: "abc-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow,
            revoked: null);

        // Assert
        token.Revoked.Should().BeNull();
    }

    #endregion

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
