using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Validation;
using ThePredictions.Domain.Models;
using System.Security.Claims;

namespace ThePredictions.Application.Features.Authentication.Commands.LoginWithGoogle;

public class LoginWithGoogleCommandHandler(
    IUserManager userManager,
    IAuthenticationTokenService tokenService,
    IDateTimeProvider dateTimeProvider,
    ILogger<LoginWithGoogleCommandHandler> logger)
    : IRequestHandler<LoginWithGoogleCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(LoginWithGoogleCommand request, CancellationToken cancellationToken)
    {
        const string provider = "Google";

        if (!request.AuthenticateResult.Succeeded || request.AuthenticateResult.Principal == null)
        {
            logger.LogInformation("Google sign-in failed: the external authentication result did not succeed.");
            return new ExternalLoginFailedAuthenticationResponse("External authentication failed.", request.Source);
        }

        var principal = request.AuthenticateResult.Principal;
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        Guard.Against.NullOrWhiteSpace(providerKey, message: "Could not determine user identifier from external provider.");

        var user = await userManager.FindByLoginAsync(provider, providerKey);
        if (user == null)
        {
            var email = principal.FindFirstValue(ClaimTypes.Email);
            Guard.Against.NullOrWhiteSpace(email, message: "Could not retrieve email from external provider.");

            var userByEmail = await userManager.FindByEmailAsync(email);
            if (userByEmail != null)
            {
                user = await LinkExternalLoginToExistingUser(userByEmail, provider, providerKey);
                logger.LogInformation("Google sign-in linked to existing User (ID: {UserId}).", user.Id);
            }
            else
            {
                user = await CreateNewUserFromExternalLogin(principal, provider, providerKey);
                logger.LogInformation("Google sign-in created new User (ID: {UserId}).", user.Id);
            }
        }

        var (accessToken, refreshToken, expiresAtUtc) = await tokenService.GenerateTokensAsync(user, cancellationToken);

        logger.LogInformation("Google sign-in succeeded for User (ID: {UserId}).", user.Id);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAtUtc: expiresAtUtc
        );
    }

    private async Task<ApplicationUser> CreateNewUserFromExternalLogin(ClaimsPrincipal principal, string provider, string providerKey)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)!;

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = NameValidator.Sanitize(principal.FindFirstValue(ClaimTypes.GivenName)),
            LastName = NameValidator.Sanitize(principal.FindFirstValue(ClaimTypes.Surname)),
            EmailConfirmed = true
        };

        newUser.RecordRegistrationConsent(marketingOptIn: false, dateTimeProvider.UtcNow);

        var createResult = await userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
            throw new IdentityUpdateException(createResult.Errors);

        await userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        var addLoginResult = await userManager.AddLoginAsync(newUser,provider, providerKey);
        return !addLoginResult.Succeeded ? throw new IdentityUpdateException(addLoginResult.Errors) : newUser;
    }

    private async Task<ApplicationUser> LinkExternalLoginToExistingUser(ApplicationUser user, string provider, string providerKey)
    {
        var addLoginResult = await userManager.AddLoginAsync(user, provider, providerKey);
        return !addLoginResult.Succeeded ? throw new IdentityUpdateException(addLoginResult.Errors) : user;
    }
}