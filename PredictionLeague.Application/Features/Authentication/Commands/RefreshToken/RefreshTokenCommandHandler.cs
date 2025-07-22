using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationTokenService _tokenService;

    public RefreshTokenCommandHandler(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthenticationTokenService tokenService)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return new FailedAuthenticationResponse("Refresh token not found.");

        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (storedToken is not { IsActive: true })
            return new FailedAuthenticationResponse("Invalid or expired refresh token.");

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
            return new FailedAuthenticationResponse("User not found.");

        var (accessToken, newRefreshToken, expiresAt) = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        return new SuccessfulAuthenticationResponse(accessToken, expiresAt, newRefreshToken);
    }
}