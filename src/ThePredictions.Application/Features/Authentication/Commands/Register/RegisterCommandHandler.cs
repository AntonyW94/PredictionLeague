using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Authentication.Commands.Register;

public class RegisterCommandHandler(
    IUserManager userManager,
    IAuthenticationTokenService tokenService,
    IEmailConfirmationSender emailConfirmationSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<RegisterCommandHandler> logger)
    : IRequestHandler<RegisterCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userExists = await userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
        {
            logger.LogInformation("Registration rejected: an account already exists for User (ID: {UserId}).", userExists.Id);
            return new FailedAuthenticationResponse("Registration could not be completed. If you already have an account, please try logging in.");
        }

        var newUser = ApplicationUser.Create(
            request.FirstName,
            request.LastName,
            request.Email
        );

        newUser.RecordRegistration(request.MarketingOptIn, dateTimeProvider.UtcNow);

        var result = await userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
            throw new Common.Exceptions.IdentityUpdateException(result.Errors);

        await userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        // Issue + email a confirmation link. Resilient: never blocks registration on email delivery.
        await emailConfirmationSender.SendAsync(newUser, cancellationToken);

        var (accessToken, refreshToken, expiresAtUtc) = await tokenService.GenerateTokensAsync(newUser, cancellationToken);

        logger.LogInformation("Registration completed for User (ID: {UserId}); marketing opt-in {MarketingOptIn}.", newUser.Id, request.MarketingOptIn);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAtUtc: expiresAtUtc
        );
    }
}
