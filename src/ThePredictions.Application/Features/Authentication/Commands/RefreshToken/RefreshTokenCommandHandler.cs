using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler(
    IUserManager userManager,
    IRefreshTokenRepository refreshTokenRepository,
    IAuthenticationTokenService tokenService,
    IDateTimeProvider dateTimeProvider,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    // How long a just-rotated token keeps working. Long enough to absorb a burst of
    // near-simultaneous refreshes from multiple browser tabs (which share the cookie),
    // short enough that a leaked token can't be replayed long after rotation.
    private static readonly TimeSpan ReuseGraceWindow = TimeSpan.FromSeconds(30);

    public async Task<AuthenticationResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("RefreshTokenCommandHandler started.");

        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            logger.LogWarning("Handler received a null or empty refresh token.");
            return new FailedAuthenticationResponse("Refresh token not found.");
        }

        var correctedToken = request.RefreshToken.Replace(' ', '+');
        logger.LogDebug("Token format corrected (space replacement applied)");

        var storedToken = await refreshTokenRepository.GetByTokenAsync(correctedToken, cancellationToken);
        if (storedToken == null)
        {
            logger.LogWarning("Refresh token validation failed - token not found");
            return new FailedAuthenticationResponse("Invalid or expired refresh token.");
        }

        var isActive = storedToken.IsActive(dateTimeProvider);
        if (!isActive && !storedToken.IsWithinReuseGrace(dateTimeProvider, ReuseGraceWindow))
        {
            logger.LogWarning("Refresh token validation failed - token not found or inactive");
            return new FailedAuthenticationResponse("Invalid or expired refresh token.");
        }

        if (!isActive)
            logger.LogInformation("Refresh token was rotated within the grace window; re-issuing for user ID: {UserId} without logging out", storedToken.UserId);
        else
            logger.LogInformation("Successfully found active token in the database for user ID: {UserId}", storedToken.UserId);

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            logger.LogError("User not found for UserId: {UserId} associated with the refresh token.", storedToken.UserId);
            return new FailedAuthenticationResponse("User not found.");
        }
        logger.LogInformation("Successfully found User (ID: {UserId})", user.Id);

        // Only rotate (revoke) a token that is still active. A grace-window token is
        // already revoked from its original rotation - revoking it again would push its
        // grace window forward and let it be reused indefinitely.
        if (isActive)
        {
            storedToken.Revoke(dateTimeProvider);
            await refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        }

        var (accessToken, newRefreshToken, expiresAt) = await tokenService.GenerateTokensAsync(user, cancellationToken);
        logger.LogInformation("Successfully generated new tokens for User (ID: {UserId})", user.Id);

        return new SuccessfulAuthenticationResponse(accessToken, expiresAt, newRefreshToken);
    }
}
