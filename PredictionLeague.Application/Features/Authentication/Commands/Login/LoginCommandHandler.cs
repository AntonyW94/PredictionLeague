using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthenticationResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationTokenService _tokenService;

    public LoginCommandHandler(UserManager<ApplicationUser> userManager, IAuthenticationTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return new AuthenticationResponse { IsSuccess = false, Message = "Invalid email or password." };

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