using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Authentication.Commands.ResendConfirmation;

public class ResendConfirmationCommandHandler(
    IUserManager userManager,
    IEmailConfirmationTokenRepository tokenRepository,
    IEmailConfirmationSender emailConfirmationSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<ResendConfirmationCommandHandler> logger)
    : IRequestHandler<ResendConfirmationCommand, Unit>
{
    private const int MaxRequestsPerHour = 3;

    public async Task<Unit> Handle(ResendConfirmationCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Unit.Value;

        // Nothing to do if already confirmed.
        if (user.EmailConfirmed)
            return Unit.Value;

        var recentRequestCount = await tokenRepository.CountByUserIdSinceAsync(
            user.Id,
            dateTimeProvider.UtcNow.AddHours(-1),
            cancellationToken);

        if (recentRequestCount >= MaxRequestsPerHour)
        {
            logger.LogWarning("Email confirmation resend rate limit exceeded for User (ID: {UserId})", user.Id);
            return Unit.Value;
        }

        await emailConfirmationSender.SendAsync(user, cancellationToken);

        return Unit.Value;
    }
}
