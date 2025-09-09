using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Common.Exceptions;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using System.Security.Claims;

namespace PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;

public class LoginWithGoogleCommandHandler : IRequestHandler<LoginWithGoogleCommand, AuthenticationResponse>
{
    private readonly IUserManager _userManager;
    private readonly IAuthenticationTokenService _tokenService;

    public LoginWithGoogleCommandHandler(IUserManager userManager, IAuthenticationTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(LoginWithGoogleCommand request, CancellationToken cancellationToken)
    {
        const string provider = "Google";

        if (!request.AuthenticateResult.Succeeded || request.AuthenticateResult.Principal == null)
            return new ExternalLoginFailedAuthenticationResponse("External authentication failed.", request.Source);

        var principal = request.AuthenticateResult.Principal;
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        Guard.Against.NullOrWhiteSpace(providerKey, message: "Could not determine user identifier from external provider.");

        var user = await _userManager.FindByLoginAsync(provider, providerKey);
        if (user == null)
        {
            var email = principal.FindFirstValue(ClaimTypes.Email);
            Guard.Against.NullOrWhiteSpace(email, message: "Could not retrieve email from external provider.");

            var userByEmail = await _userManager.FindByEmailAsync(email);
            if (userByEmail != null)
                user = await LinkExternalLoginToExistingUser(userByEmail, provider, providerKey);
            else
                user = await CreateNewUserFromExternalLogin(principal, provider, providerKey);
        }

        var (accessToken, refreshToken, expiresAt) = await _tokenService.GenerateTokensAsync(user, cancellationToken);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAt: expiresAt
        );
    }

    private async Task<ApplicationUser> CreateNewUserFromExternalLogin(ClaimsPrincipal principal, string provider, string providerKey)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)!;

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
            LastName = principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
            throw new IdentityUpdateException(createResult.Errors);

        await _userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        var addLoginResult = await _userManager.AddLoginAsync(newUser,provider, providerKey);
        return !addLoginResult.Succeeded ? throw new IdentityUpdateException(addLoginResult.Errors) : newUser;
    }

    private async Task<ApplicationUser> LinkExternalLoginToExistingUser(ApplicationUser user, string provider, string providerKey)
    {
        var addLoginResult = await _userManager.AddLoginAsync(user, provider, providerKey);
        return !addLoginResult.Succeeded ? throw new IdentityUpdateException(addLoginResult.Errors) : user;
    }
}