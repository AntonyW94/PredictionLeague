using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationTokenService _tokenService;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        UserManager<ApplicationUser> userManager,
        IAuthenticationTokenService tokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return new AuthenticationResponse { IsSuccess = false, Message = "Refresh token not found." };

        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);
        if (storedToken is not { IsActive: true })
            return new AuthenticationResponse { IsSuccess = false, Message = "Invalid or expired refresh token." };
        
        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
            return new AuthenticationResponse { IsSuccess = false, Message = "User not found." };
        
        return await _tokenService.GenerateAccessToken(user);
    }
}