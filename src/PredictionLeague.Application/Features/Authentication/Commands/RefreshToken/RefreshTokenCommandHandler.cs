using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Common;

namespace PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private readonly IUserManager _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserManager userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthenticationTokenService tokenService,
        IDateTimeProvider dateTimeProvider,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<AuthenticationResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RefreshTokenCommandHandler started.");

        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            _logger.LogWarning("Handler received a null or empty refresh token.");
            return new FailedAuthenticationResponse("Refresh token not found.");
        }

        var correctedToken = request.RefreshToken.Replace(' ', '+');
        _logger.LogDebug("Token format corrected (space replacement applied)");

        var storedToken = await _refreshTokenRepository.GetByTokenAsync(correctedToken, cancellationToken);
        if (storedToken == null || !storedToken.IsActive(_dateTimeProvider))
        {
            _logger.LogWarning("Refresh token validation failed - token not found or inactive");
            return new FailedAuthenticationResponse("Invalid or expired refresh token.");
        }
        _logger.LogInformation("Successfully found active token in the database for user ID: {UserId}", storedToken.UserId);

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            _logger.LogError("User not found for UserId: {UserId} associated with the refresh token.", storedToken.UserId);
            return new FailedAuthenticationResponse("User not found.");
        }
        _logger.LogInformation("Successfully found User (ID: {UserId})", user.Id);

        storedToken.Revoke(_dateTimeProvider);
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);

        var (accessToken, newRefreshToken, expiresAt) = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        _logger.LogInformation("Successfully generated new tokens for User (ID: {UserId})", user.Id);

        return new SuccessfulAuthenticationResponse(accessToken, expiresAt, newRefreshToken);
    }
}
