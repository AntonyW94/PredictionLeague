using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Authentication.Commands.RefreshToken;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuthenticationTokenService _tokenService = Substitute.For<IAuthenticationTokenService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly ILogger<RefreshTokenCommandHandler> _logger = Substitute.For<ILogger<RefreshTokenCommandHandler>>();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _userManager,
            _refreshTokenRepository,
            _tokenService,
            _dateTimeProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessfulResponse_WhenTokenIsValid()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-refresh-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: "valid-refresh-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: null);
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };
        var newExpiresAtUtc = _dateTimeProvider.UtcNow.AddHours(1);

        _refreshTokenRepository.GetByTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("new-access-token", "new-refresh-token", newExpiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        var success = (SuccessfulAuthenticationResponse)result;
        success.AccessToken.Should().Be("new-access-token");
        success.RefreshTokenForCookie.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenIsNullOrEmpty()
    {
        // Arrange
        var command = new RefreshTokenCommand("");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("Refresh token not found.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenNotFoundInDatabase()
    {
        // Arrange
        var command = new RefreshTokenCommand("unknown-token");

        _refreshTokenRepository.GetByTokenAsync("unknown-token", Arg.Any<CancellationToken>())
            .Returns((Domain.Models.RefreshToken?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenIsExpired()
    {
        // Arrange
        var command = new RefreshTokenCommand("expired-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: "expired-token",
            expires: _dateTimeProvider.UtcNow.AddDays(-1),
            created: _dateTimeProvider.UtcNow.AddDays(-8),
            revoked: null);

        _refreshTokenRepository.GetByTokenAsync("expired-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenIsRevoked()
    {
        // Arrange
        var command = new RefreshTokenCommand("revoked-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: "revoked-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: _dateTimeProvider.UtcNow.AddHours(-1));

        _refreshTokenRepository.GetByTokenAsync("revoked-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenUserNotFound()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-refresh-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "deleted-user", token: "valid-refresh-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: null);

        _refreshTokenRepository.GetByTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userManager.FindByIdAsync("deleted-user").Returns((ApplicationUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("User not found.");
    }

    [Fact]
    public async Task Handle_ShouldRevokeOldTokenAndUpdateIt_WhenTokenIsValid()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-refresh-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: "valid-refresh-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: null);
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };
        var newExpiresAtUtc = _dateTimeProvider.UtcNow.AddHours(1);

        _refreshTokenRepository.GetByTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("new-access-token", "new-refresh-token", newExpiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _refreshTokenRepository.Received(1).UpdateAsync(storedToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReissueWithoutRevoking_WhenTokenWasRotatedWithinGraceWindow()
    {
        // Arrange - a sibling tab rotated this token 5 seconds ago (still within the grace window)
        var command = new RefreshTokenCommand("just-rotated-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: "just-rotated-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: _dateTimeProvider.UtcNow.AddSeconds(-5));
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };
        var newExpiresAtUtc = _dateTimeProvider.UtcNow.AddHours(1);

        _refreshTokenRepository.GetByTokenAsync("just-rotated-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("new-access-token", "new-refresh-token", newExpiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - the caller gets working tokens and is not logged out, and the already
        // revoked token is not revoked/updated again (which would extend its grace window).
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        ((SuccessfulAuthenticationResponse)result).AccessToken.Should().Be("new-access-token");
        await _refreshTokenRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenRevokedBeyondGraceWindow()
    {
        // Arrange - revoked an hour ago, well beyond the grace window
        var command = new RefreshTokenCommand("long-revoked-token");
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: "long-revoked-token",
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: _dateTimeProvider.UtcNow.AddHours(-1));

        _refreshTokenRepository.GetByTokenAsync("long-revoked-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        ((FailedAuthenticationResponse)result).Message.Should().Be("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Handle_ShouldReplaceSpacesWithPlusInToken_WhenTokenContainsSpaces()
    {
        // Arrange
        var command = new RefreshTokenCommand("token with spaces");
        var correctedToken = "token+with+spaces";
        var storedToken = new Domain.Models.RefreshToken(
            id: 1, userId: "user-1", token: correctedToken,
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: null);
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };
        var newExpiresAtUtc = _dateTimeProvider.UtcNow.AddHours(1);

        _refreshTokenRepository.GetByTokenAsync(correctedToken, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("new-access-token", "new-refresh-token", newExpiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        await _refreshTokenRepository.Received(1).GetByTokenAsync(correctedToken, Arg.Any<CancellationToken>());
    }
}
