using Ardalis.GuardClauses;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Application.Common.Exceptions;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using System.Security.Claims;

namespace PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;

public class LoginWithGoogleCommandHandler : IRequestHandler<LoginWithGoogleCommand, AuthenticationResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public LoginWithGoogleCommandHandler(UserManager<ApplicationUser> userManager, IAuthenticationTokenService tokenService, IConfiguration configuration)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _configuration = configuration;
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

            user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
                    LastName = principal.FindFirstValue(ClaimTypes.Surname) ?? "",
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(newUser);
                if (!createResult.Succeeded)
                    throw new IdentityUpdateException(createResult.Errors);

                await _userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

                var addLoginResult = await _userManager.AddLoginAsync(newUser, new UserLoginInfo(provider, providerKey, provider));
                if (!addLoginResult.Succeeded)
                    throw new IdentityUpdateException(addLoginResult.Errors);

                user = newUser;
            }
            else
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
                if (!addLoginResult.Succeeded)
                    throw new IdentityUpdateException(addLoginResult.Errors);
            }
        }
        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user);

        var expiryMinutes = double.Parse(_configuration["JwtSettings:ExpiryMinutes"]!);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAt: expiresAt
        );
    }
}