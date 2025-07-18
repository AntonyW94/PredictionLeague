using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private readonly ILogger<RefreshTokenCommandHandler> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationTokenService _tokenService;

    public RefreshTokenCommandHandler(
        ILogger<RefreshTokenCommandHandler> logger,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthenticationTokenService tokenService)
    {
        _logger = logger;
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return new AuthenticationResponse { IsSuccess = false, Message = "Refresh token not found." };

        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);
        if (storedToken is not { IsActive: true })
        {
            _logger.LogWarning("Invalid or expired refresh token presented: {RefreshToken}", request.RefreshToken);
            return new AuthenticationResponse { IsSuccess = false, Message = "Invalid or expired refresh token." };
        }
        
        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            _logger.LogWarning("User (ID: {UserId}) from a valid refresh token was not found.", storedToken.UserId);
            return new AuthenticationResponse { IsSuccess = false, Message = "User not found." };
        }
        
        return await _tokenService.GenerateAccessToken(user);
    }
}