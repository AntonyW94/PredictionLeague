using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Authentication.Commands.ConfirmEmail;

public class ConfirmEmailCommandHandler(
    IEmailConfirmationTokenRepository tokenRepository,
    IUserManager userManager,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ConfirmEmailCommand, Unit>
{
    private const string InvalidLinkMessage = "This confirmation link is invalid or has expired. Please request a new one.";

    public async Task<Unit> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var token = await tokenRepository.GetByTokenAsync(request.Token, cancellationToken);
        if (token is null)
            throw new BusinessRuleViolationException(InvalidLinkMessage);

        if (token.IsExpired(dateTimeProvider))
        {
            await tokenRepository.DeleteByUserIdAsync(token.UserId, cancellationToken);
            throw new BusinessRuleViolationException(InvalidLinkMessage);
        }

        var user = await userManager.FindByIdAsync(token.UserId);
        if (user is null)
            throw new BusinessRuleViolationException(InvalidLinkMessage);

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        await tokenRepository.DeleteByUserIdAsync(token.UserId, cancellationToken);

        return Unit.Value;
    }
}
