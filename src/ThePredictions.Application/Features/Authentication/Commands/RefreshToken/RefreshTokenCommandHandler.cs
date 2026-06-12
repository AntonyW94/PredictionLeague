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
        logger.LogDebug("RefreshTokenCommandHandler started.");

        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            // Expected: the caller had no token to present. The client treats this as
            // "please log in" - it is not an application fault, so log at Information.
            logger.LogInformation("Refresh request had no token; the client will be asked to log in.");
            return new FailedAuthenticationResponse("Refresh token not found.");
        }

        var correctedToken = request.RefreshToken.Replace(' ', '+');
        logger.LogDebug("Token format corrected (space replacement applied)");

        var storedToken = await refreshTokenRepository.GetByTokenAsync(correctedToken, cancellationToken);
        if (storedToken == null)
        {
            // Expected end-of-session: the token expired, was rotated long ago, the cookie
            // was cleared, or the user logged out. Not an error - the client re-authenticates.
            logger.LogInformation("Refresh token not recognised (expired, rotated, or cleared); the client will be asked to log in.");
            return new FailedAuthenticationResponse("Invalid or expired refresh token.");
        }

        var isActive = storedToken.IsActive(dateTimeProvider);
        if (!isActive && !storedToken.IsWithinReuseGrace(dateTimeProvider, ReuseGraceWindow))
        {
            // Same as above - a genuinely inactive (expired/revoked) token is a normal
            // end-of-session, so this is Information rather than a warning.
            logger.LogInformation("Refresh token is no longer active; the client will be asked to log in.");
            return new FailedAuthenticationResponse("Invalid or expired refresh token.");
        }

        if (!isActive)
            logger.LogDebug("Refresh token was rotated within the grace window; re-issuing for user ID: {UserId} without logging out", storedToken.UserId);
        else
            logger.LogDebug("Found active refresh token for user ID: {UserId}", storedToken.UserId);

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            // Genuinely unexpected: a live token whose user no longer exists. Worth a warning
            // (data integrity), but it does not happen in normal operation.
            logger.LogWarning("Refresh token references User (ID: {UserId}) that no longer exists.", storedToken.UserId);
            return new FailedAuthenticationResponse("User not found.");
        }
        logger.LogDebug("Found User (ID: {UserId})", user.Id);

        // Only rotate (revoke) a token that is still active. A grace-window token is
        // already revoked from its original rotation - revoking it again would push its
        // grace window forward and let it be reused indefinitely.
        if (isActive)
        {
            storedToken.Revoke(dateTimeProvider);
            await refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        }

        var (accessToken, newRefreshToken, expiresAt) = await tokenService.GenerateTokensAsync(user, cancellationToken);
        logger.LogDebug("Generated new tokens for User (ID: {UserId})", user.Id);

        return new SuccessfulAuthenticationResponse(accessToken, expiresAt, newRefreshToken);
    }
}
