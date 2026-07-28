using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Authentication.Commands.LoginWithGoogle;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class LoginWithGoogleCommandHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 4, 28, 10, 30, 0, DateTimeKind.Utc);

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IAuthenticationTokenService _tokenService = Substitute.For<IAuthenticationTokenService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(FixedNowUtc);
    private readonly LoginWithGoogleCommandHandler _handler;

    private static readonly DateTime ExpiresAtUtc = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    public LoginWithGoogleCommandHandlerTests()
    {
        _handler = new LoginWithGoogleCommandHandler(_userManager, _tokenService, _dateTimeProvider, Substitute.For<ILogger<LoginWithGoogleCommandHandler>>());
    }

    private static AuthenticateResult CreateSuccessfulAuthResult(
        string nameIdentifier = "google-id-123",
        string? email = "john@example.com",
        string? givenName = "John",
        string? surname = "Doe")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, nameIdentifier)
        };

        if (email != null)
            claims.Add(new Claim(ClaimTypes.Email, email));
        if (givenName != null)
            claims.Add(new Claim(ClaimTypes.GivenName, givenName));
        if (surname != null)
            claims.Add(new Claim(ClaimTypes.Surname, surname));

        var identity = new ClaimsIdentity(claims, "Google");
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, "Google"));
    }

    [Fact]
    public async Task Handle_ShouldReturnExternalLoginFailedResponse_WhenAuthenticationFails()
    {
        // Arrange
        var failedResult = AuthenticateResult.Fail("Auth failed");
        var command = new LoginWithGoogleCommand(failedResult, "login");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExternalLoginFailedAuthenticationResponse>();
        var failure = (ExternalLoginFailedAuthenticationResponse)result;
        failure.Message.Should().Be("External authentication failed.");
        failure.Source.Should().Be("login");
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessfulResponse_WhenExistingUserFoundByLogin()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "login");
        var existingUser = new ApplicationUser { Id = "user-1", Email = "john@example.com" };

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns(existingUser);
        _tokenService.GenerateTokensAsync(existingUser, Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", ExpiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        var success = (SuccessfulAuthenticationResponse)result;
        success.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task Handle_ShouldCreateNewUser_WhenNoExistingUserFound()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "register");

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), nameof(ApplicationUserRole.Player))
            .Returns(UserManagerResult.Success());
        _userManager.AddLoginAsync(Arg.Any<ApplicationUser>(), "Google", "google-id-123")
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", ExpiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        await _userManager.Received(1).CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task Handle_ShouldLinkExternalLogin_WhenUserExistsByEmailButNotByLogin()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "login");
        var existingUser = new ApplicationUser { Id = "user-1", Email = "john@example.com" };

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns(existingUser);
        _userManager.AddLoginAsync(existingUser, "Google", "google-id-123")
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(existingUser, Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", ExpiresAtUtc));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SuccessfulAuthenticationResponse>();
        await _userManager.Received(1).AddLoginAsync(existingUser, "Google", "google-id-123");
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task Handle_ShouldThrowIdentityUpdateException_WhenCreateNewUserFails()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "register");

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>())
            .Returns(UserManagerResult.Failure(new[] { "Creation failed" }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IdentityUpdateException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowIdentityUpdateException_WhenAddLoginFailsForNewUser()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "register");

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), nameof(ApplicationUserRole.Player))
            .Returns(UserManagerResult.Success());
        _userManager.AddLoginAsync(Arg.Any<ApplicationUser>(), "Google", "google-id-123")
            .Returns(UserManagerResult.Failure(new[] { "Link failed" }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IdentityUpdateException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowIdentityUpdateException_WhenLinkExternalLoginFails()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "login");
        var existingUser = new ApplicationUser { Id = "user-1", Email = "john@example.com" };

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns(existingUser);
        _userManager.AddLoginAsync(existingUser, "Google", "google-id-123")
            .Returns(UserManagerResult.Failure(new[] { "Link failed" }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IdentityUpdateException>();
    }

    [Fact]
    public async Task Handle_ShouldStampTermsAcceptedTimestamp_WhenCreatingNewUser()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "register");

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns((ApplicationUser?)null);
        ApplicationUser? capturedUser = null;
        _userManager.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u))
            .Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _userManager.AddLoginAsync(Arg.Any<ApplicationUser>(), "Google", "google-id-123")
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", ExpiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.TermsAcceptedAtUtc.Should().Be(FixedNowUtc);
        capturedUser.MarketingOptInAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldAssignPlayerRole_WhenCreatingNewUser()
    {
        // Arrange
        var authResult = CreateSuccessfulAuthResult();
        var command = new LoginWithGoogleCommand(authResult, "register");

        _userManager.FindByLoginAsync("Google", "google-id-123").Returns((ApplicationUser?)null);
        _userManager.FindByEmailAsync("john@example.com").Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(UserManagerResult.Success());
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Success());
        _userManager.AddLoginAsync(Arg.Any<ApplicationUser>(), "Google", "google-id-123")
            .Returns(UserManagerResult.Success());
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", ExpiresAtUtc));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userManager.Received(1).AddToRoleAsync(
            Arg.Any<ApplicationUser>(),
            nameof(ApplicationUserRole.Player));
    }
}
