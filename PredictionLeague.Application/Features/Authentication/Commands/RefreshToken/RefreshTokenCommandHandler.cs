using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private readonly IUserManager _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserManager userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthenticationTokenService tokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
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
        _logger.LogInformation("Corrected token by replacing spaces with '+': {CorrectedToken}", correctedToken);

        var storedToken = await _refreshTokenRepository.GetByTokenAsync(correctedToken, cancellationToken);
        if (storedToken is not { IsActive: true })
        {
            _logger.LogWarning("GetByTokenAsync returned null or the token is not active. Token provided: {CorrectedToken}", correctedToken);
            return new FailedAuthenticationResponse($"Invalid or expired refresh token. ({correctedToken})");
        }
        _logger.LogInformation("Successfully found active token in the database for user ID: {UserId}", storedToken.UserId);

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            _logger.LogError("User not found for UserId: {UserId} associated with the refresh token.", storedToken.UserId);
            return new FailedAuthenticationResponse("User not found.");
        }
        _logger.LogInformation("Successfully found user: {Email}", user.Email);

        storedToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);

        var (accessToken, newRefreshToken, expiresAt) = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        _logger.LogInformation("Successfully generated new tokens for user: {Email}", user.Email);

        return new SuccessfulAuthenticationResponse(accessToken, expiresAt, newRefreshToken);
    }
}
