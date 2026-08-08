using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Authentication.Commands.RequestPasswordReset;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class RequestPasswordResetCommandHandlerTests
{
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger = Substitute.For<ILogger<RequestPasswordResetCommandHandler>>();
    private readonly RequestPasswordResetCommandHandler _handler;

    private readonly BrevoSettings _brevoSettings = new()
    {
        Templates = new TemplateSettings
        {
            PasswordReset = 100,
            PasswordResetGoogleUser = 200
        }
    };

    public RequestPasswordResetCommandHandlerTests()
    {
        var options = Options.Create(_brevoSettings);
        var siteOptions = Options.Create(new SiteSettings { BaseUrl = "https://test.local" });
        _handler = new RequestPasswordResetCommandHandler(
            _userManager,
            _tokenRepository,
            _emailService,
            options,
            siteOptions,
            _dateTimeProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnit_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("unknown@example.com");

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("unknown@example.com");

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnit_WhenRateLimitExceeded()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenRateLimitExceeded()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldSendPasswordResetEmail_WhenUserHasPassword()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email, FirstName = "John" };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _userManager.HasPasswordAsync(user).Returns(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendTemplatedEmailAsync(
            command.Email, 100, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldCreateResetToken_WhenUserHasPassword()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email, FirstName = "John" };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _userManager.HasPasswordAsync(user).Returns(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.Received(1).CreateAsync(
            Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendGoogleUserEmail_WhenUserDoesNotHavePassword()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email, FirstName = "John" };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _userManager.HasPasswordAsync(user).Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendTemplatedEmailAsync(
            command.Email, 200, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldNotCreateResetToken_WhenUserDoesNotHavePassword()
    {
        // Arrange
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email, FirstName = "John" };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _userManager.HasPasswordAsync(user).Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.DidNotReceive().CreateAsync(
            Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    private RequestPasswordResetCommandHandler HandlerWithNoTemplates() =>
        new(_userManager, _tokenRepository, _emailService,
            Options.Create(new BrevoSettings { Templates = null }),
            Options.Create(new SiteSettings { BaseUrl = "https://test.local" }),
            _dateTimeProvider, _logger);

    [Fact]
    public async Task Handle_ShouldThrow_WhenThePasswordResetTemplateIsNotConfigured()
    {
        // Silently doing nothing would leave someone waiting for an email that never arrives, so
        // this fails loudly for the operator instead.
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email, FirstName = "John" };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        _userManager.HasPasswordAsync(user).Returns(true);

        var act = () => HandlerWithNoTemplates().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PasswordReset*");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheGoogleSignInTemplateIsNotConfigured()
    {
        var command = new RequestPasswordResetCommand("john@example.com");
        var user = new ApplicationUser { Id = "user-1", Email = command.Email, FirstName = "John" };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _tokenRepository.CountByUserIdSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        _userManager.HasPasswordAsync(user).Returns(false);

        var act = () => HandlerWithNoTemplates().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PasswordResetGoogleUser*");
    }
}
