using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;
using System.Security.Claims;

namespace PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;

public class LoginWithGoogleCommandHandler : IRequestHandler<LoginWithGoogleCommand, AuthenticationResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationTokenService _tokenService;

    public LoginWithGoogleCommandHandler(UserManager<ApplicationUser> userManager, IAuthenticationTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(LoginWithGoogleCommand request, CancellationToken cancellationToken)
    {
        var externalLoginInfo = new UserLoginInfo(request.LoginProvider, request.ProviderKey, request.LoginProvider);

        var user = await _userManager.FindByLoginAsync(externalLoginInfo.LoginProvider, externalLoginInfo.ProviderKey);
        if (user == null)
        {
            var email = request.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
                return new AuthenticationResponse { IsSuccess = false, Message = "Could not retrieve email from external provider." };

            user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = request.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
                    LastName = request.Principal.FindFirstValue(ClaimTypes.Surname) ?? "",
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return new AuthenticationResponse { IsSuccess = false, Message = "There was a problem creating your account." };

                await _userManager.AddToRoleAsync(user, nameof(ApplicationUserRole.Player));
            }

            await _userManager.AddLoginAsync(user, externalLoginInfo);
        }

        var accessTokenResponse = await _tokenService.GenerateAccessToken(user);
        var refreshTokenEntity = await _tokenService.GenerateAndStoreRefreshToken(user);

        return new AuthenticationResponse
        {
            IsSuccess = accessTokenResponse.IsSuccess,
            Message = accessTokenResponse.Message,
            Token = accessTokenResponse.Token,
            ExpiresAt = accessTokenResponse.ExpiresAt,
            RefreshTokenForCookie = refreshTokenEntity.Token
        };
    }
}