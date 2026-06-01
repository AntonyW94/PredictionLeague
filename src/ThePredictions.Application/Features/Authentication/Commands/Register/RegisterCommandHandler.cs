using MediatR;
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
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RegisterCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userExists = await userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return new FailedAuthenticationResponse("Registration could not be completed. If you already have an account, please try logging in.");

        var newUser = ApplicationUser.Create(
            request.FirstName,
            request.LastName,
            request.Email
        );

        newUser.RecordRegistrationConsent(request.MarketingOptIn, dateTimeProvider.UtcNow);

        var result = await userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
            throw new Common.Exceptions.IdentityUpdateException(result.Errors);

        await userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        // Issue + email a confirmation link. Resilient: never blocks registration on email delivery.
        await emailConfirmationSender.SendAsync(newUser, request.ConfirmUrlBase, cancellationToken);

        var (accessToken, refreshToken, expiresAtUtc) = await tokenService.GenerateTokensAsync(newUser, cancellationToken);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAtUtc: expiresAtUtc
        );
    }
}
