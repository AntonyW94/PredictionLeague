using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthenticationResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public LoginCommandHandler(UserManager<ApplicationUser> userManager, IAuthenticationTokenService tokenService, IConfiguration configuration)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<AuthenticationResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return new FailedAuthenticationResponse("Invalid email or password.");

        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        var expiryMinutes = double.Parse(_configuration["JwtSettings:ExpiryMinutes"]!);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAt: expiresAt
        );
    }
}