using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Authentication.Commands.Register;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class RegisterCommandHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 4, 28, 10, 30, 0, DateTimeKind.Utc);

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IAuthenticationTokenService _tokenService = Substitute.For<IAuthenticationTokenService>();
    private readonly IEmailConfirmationSender _emailConfirmationSender = Substitute.For<IEmailConfirmationSender>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(FixedNowUtc);
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _handler = new RegisterCommandHandler(_userManager, _tokenService, _emailConfirmationSender, _dateTimeProvider);
    }

    private static RegisterCommand BuildCommand(string email = "john@example.com", bool marketingOptIn = false) =>
        new("John", "Doe", email, "Password123!", marketingOptIn);

    [Fact]
    public async Task Handle_ShouldReturnSuccessfulResponse_WhenRegistrationIsValid()
    {
        // Arrange
        var command = BuildCommand();
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), nameof(ApplicationUserRole.Player))
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
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
    public async Task Handle_ShouldReturnFailedResponse_WhenEmailAlreadyExists()
    {
        // Arrange
        var command = BuildCommand("existing@example.com");
        var existingUser = new ApplicationUser { Email = command.Email };

        _userManager.FindByEmailAsync(command.Email).Returns(existingUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FailedAuthenticationResponse>();
        var failure = (FailedAuthenticationResponse)result;
        failure.Message.Should().Contain("Registration could not be completed");
    }

    [Fact]
    public async Task Handle_ShouldNotCreateUser_WhenEmailAlreadyExists()
    {
        // Arrange
        var command = BuildCommand("existing@example.com");
        var existingUser = new ApplicationUser { Email = command.Email };

        _userManager.FindByEmailAsync(command.Email).Returns(existingUser);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldThrowIdentityUpdateException_WhenCreateFails()
    {
        // Arrange
        var command = BuildCommand();

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(UserManagerResult.Failure(new[] { "Password too weak" }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IdentityUpdateException>();
    }

    [Fact]
    public async Task Handle_ShouldAssignPlayerRole_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = BuildCommand();
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userManager.Received(1).AddToRoleAsync(
            Arg.Any<ApplicationUser>(),
            nameof(ApplicationUserRole.Player));
    }

    [Fact]
    public async Task Handle_ShouldGenerateTokens_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = BuildCommand();
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenService.Received(1).GenerateTokensAsync(
            Arg.Any<ApplicationUser>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendConfirmationEmail_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = BuildCommand();
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailConfirmationSender.Received(1).SendAsync(
            Arg.Any<ApplicationUser>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStampTermsAcceptedTimestamp_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = BuildCommand(marketingOptIn: false);
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        ApplicationUser? capturedUser = null;
        _userManager.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), command.Password)
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.TermsAcceptedAtUtc.Should().Be(FixedNowUtc);
        capturedUser.MarketingOptInAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldStampMarketingOptInTimestamp_WhenUserOptedIn()
    {
        // Arrange
        var command = BuildCommand(marketingOptIn: true);
        var expiresAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        ApplicationUser? capturedUser = null;
        _userManager.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), command.Password)
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.TermsAcceptedAtUtc.Should().Be(FixedNowUtc);
        capturedUser.MarketingOptInAtUtc.Should().Be(FixedNowUtc);
    }
}
