using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Authentication.Commands.Logout;

public class LogoutCommandHandler(IRefreshTokenRepository refreshTokenRepository, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Per-device logout: revoke only the refresh token presented by this device
        // (from its cookie), so signing out on one device leaves other devices' sessions
        // intact. The access token is stateless and expires on its own shortly after.
        if (string.IsNullOrEmpty(request.RefreshToken))
            return;

        var storedToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken.Replace(' ', '+'), cancellationToken);
        if (storedToken == null || storedToken.UserId != request.UserId || storedToken.Revoked != null)
            return;

        storedToken.Revoke(dateTimeProvider);
        await refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
    }
}

