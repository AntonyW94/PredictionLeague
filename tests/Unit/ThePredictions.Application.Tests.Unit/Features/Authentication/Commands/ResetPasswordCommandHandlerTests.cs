using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Authentication.Commands.ResetPassword;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class ResetPasswordCommandHandlerTests
{
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IAuthenticationTokenService _tokenService = Substitute.For<IAuthenticationTokenService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly ILogger<ResetPasswordCommandHandler> _logger = Substitute.For<ILogger<ResetPasswordCommandHandler>>();
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _handler = new ResetPasswordCommandHandler(
            _tokenRepository,
            _userManager,
            _tokenService,
            _dateTimeProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessfulResponse_WhenTokenIsValidAndPasswordResets()
    {
        // Arrange
        var command = new ResetPasswordCommand("valid-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "valid-token", "user-1",
            _dateTimeProvider.UtcNow.AddMinutes(-10),
            _dateTimeProvider.UtcNow.AddHours(1));
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };
        var expiresAtUtc = _dateTimeProvider.UtcNow.AddHours(1);

        _tokenRepository.GetByTokenAsync("valid-token", Arg.Any<CancellationToken>()).Returns(resetToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.ResetPasswordDirectAsync(user, command.NewPassword)
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulResetPasswordResponse>();
        var success = (SuccessfulResetPasswordResponse)result;
        success.AccessToken.Should().Be("access-token");
        success.RefreshTokenForCookie.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenNotFound()
    {
        // Arrange
        var command = new ResetPasswordCommand("unknown-token", "NewPassword123!");

        _tokenRepository.GetByTokenAsync("unknown-token", Arg.Any<CancellationToken>())
            .Returns((PasswordResetToken?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedResetPasswordResponse>();
        var failure = (FailedResetPasswordResponse)result;
        failure.Message.Should().Contain("invalid or has expired");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenTokenIsExpired()
    {
        // Arrange
        var command = new ResetPasswordCommand("expired-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "expired-token", "user-1",
            _dateTimeProvider.UtcNow.AddHours(-2),
            _dateTimeProvider.UtcNow.AddHours(-1));

        _tokenRepository.GetByTokenAsync("expired-token", Arg.Any<CancellationToken>()).Returns(resetToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedResetPasswordResponse>();
        var failure = (FailedResetPasswordResponse)result;
        failure.Message.Should().Contain("invalid or has expired");
    }

    [Fact]
    public async Task Handle_ShouldDeleteExpiredToken_WhenTokenIsExpired()
    {
        // Arrange
        var command = new ResetPasswordCommand("expired-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "expired-token", "user-1",
            _dateTimeProvider.UtcNow.AddHours(-2),
            _dateTimeProvider.UtcNow.AddHours(-1));

        _tokenRepository.GetByTokenAsync("expired-token", Arg.Any<CancellationToken>()).Returns(resetToken);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.Received(1).DeleteAsync("expired-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenUserNotFound()
    {
        // Arrange
        var command = new ResetPasswordCommand("valid-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "valid-token", "deleted-user",
            _dateTimeProvider.UtcNow.AddMinutes(-10),
            _dateTimeProvider.UtcNow.AddHours(1));

        _tokenRepository.GetByTokenAsync("valid-token", Arg.Any<CancellationToken>()).Returns(resetToken);
        _userManager.FindByIdAsync("deleted-user").Returns((ApplicationUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedResetPasswordResponse>();
    }

    [Fact]
    public async Task Handle_ShouldDeleteTokenAndReturnFailedResponse_WhenUserNotFound()
    {
        // Arrange
        var command = new ResetPasswordCommand("valid-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "valid-token", "deleted-user",
            _dateTimeProvider.UtcNow.AddMinutes(-10),
            _dateTimeProvider.UtcNow.AddHours(1));

        _tokenRepository.GetByTokenAsync("valid-token", Arg.Any<CancellationToken>()).Returns(resetToken);
        _userManager.FindByIdAsync("deleted-user").Returns((ApplicationUser?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.Received(1).DeleteAsync("valid-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenPasswordResetFails()
    {
        // Arrange
        var command = new ResetPasswordCommand("valid-token", "weak");
        var resetToken = new PasswordResetToken(
            "valid-token", "user-1",
            _dateTimeProvider.UtcNow.AddMinutes(-10),
            _dateTimeProvider.UtcNow.AddHours(1));
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };

        _tokenRepository.GetByTokenAsync("valid-token", Arg.Any<CancellationToken>()).Returns(resetToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.ResetPasswordDirectAsync(user, command.NewPassword)
            .Returns(UserManagerResult.Failure(new[] { "Password does not meet requirements." }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedResetPasswordResponse>();
        var failure = (FailedResetPasswordResponse)result;
        failure.Message.Should().Be("Password does not meet requirements.");
    }

    [Fact]
    public async Task Handle_ShouldDeleteAllUserTokens_WhenPasswordResetSucceeds()
    {
        // Arrange
        var command = new ResetPasswordCommand("valid-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "valid-token", "user-1",
            _dateTimeProvider.UtcNow.AddMinutes(-10),
            _dateTimeProvider.UtcNow.AddHours(1));
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };
        var expiresAtUtc = _dateTimeProvider.UtcNow.AddHours(1);

        _tokenRepository.GetByTokenAsync("valid-token", Arg.Any<CancellationToken>()).Returns(resetToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.ResetPasswordDirectAsync(user, command.NewPassword)
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.Received(1).DeleteByUserIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCleanUpExpiredTokens_WhenCalled()
    {
        // Arrange
        var command = new ResetPasswordCommand("any-token", "NewPassword123!");

        _tokenRepository.GetByTokenAsync("any-token", Arg.Any<CancellationToken>())
            .Returns((PasswordResetToken?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.Received(1).DeleteExpiredTokensAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStillExplainTheFailure_WhenNoReasonWasGiven()
    {
        // Defensive: a failure with no reasons must not surface as a blank message on the screen.
        var command = new ResetPasswordCommand("valid-token", "NewPassword123!");
        var resetToken = new PasswordResetToken(
            "valid-token",
            "user-1",
            _dateTimeProvider.UtcNow.AddMinutes(-10),
            _dateTimeProvider.UtcNow.AddHours(1));
        var user = new ApplicationUser { Id = "user-1", Email = "john@example.com" };

        _tokenRepository.GetByTokenAsync("valid-token", Arg.Any<CancellationToken>()).Returns(resetToken);
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.ResetPasswordDirectAsync(user, command.NewPassword)
            .Returns(UserManagerResult.Failure([]));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeOfType<FailedResetPasswordResponse>();
        ((FailedResetPasswordResponse)result).Message.Should().Be("Password reset failed.");
    }
}
