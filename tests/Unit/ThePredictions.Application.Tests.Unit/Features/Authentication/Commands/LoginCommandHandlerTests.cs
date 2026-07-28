using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Authentication.Commands.Login;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class LoginCommandHandlerTests
{
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IAuthenticationTokenService _tokenService = Substitute.For<IAuthenticationTokenService>();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_userManager, _tokenService, Substitute.For<ILogger<LoginCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessfulResponse_WhenCredentialsAreValid()
    {
        // Arrange
        var command = new LoginCommand("john@example.com", "Password123!");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email };
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);
        _tokenService.GenerateTokensAsync(user, Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        var success = (SuccessfulAuthenticationResponse)result;
        success.AccessToken.Should().Be("access-token");
        success.RefreshTokenForCookie.Should().Be("refresh-token");
        success.ExpiresAtUtc.Should().Be(expiresAtUtc);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenUserNotFound()
    {
        // Arrange
        var command = new LoginCommand("unknown@example.com", "Password123!");

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedResponse_WhenPasswordIsIncorrect()
    {
        // Arrange
        var command = new LoginCommand("john@example.com", "WrongPassword!");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_ShouldNotGenerateTokens_WhenUserNotFound()
    {
        // Arrange
        var command = new LoginCommand("unknown@example.com", "Password123!");

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenService.DidNotReceive().GenerateTokensAsync(
            Arg.Any<ApplicationUser>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotGenerateTokens_WhenPasswordIsIncorrect()
    {
        // Arrange
        var command = new LoginCommand("john@example.com", "WrongPassword!");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenService.DidNotReceive().GenerateTokensAsync(
            Arg.Any<ApplicationUser>(),
            Arg.Any<CancellationToken>());
    }
}
