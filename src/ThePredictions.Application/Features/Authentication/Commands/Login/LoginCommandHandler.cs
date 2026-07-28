using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Authentication;

namespace ThePredictions.Application.Features.Authentication.Commands.Login;

public class LoginCommandHandler(
    IUserManager userManager,
    IAuthenticationTokenService tokenService,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // The outcome is logged rather than just the attempt: "I cannot log in" is the support
        // question this answers, and the pipeline behaviour only sees that the command ran.
        // Identifiers only - the submitted email address is never logged.
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            logger.LogInformation("Login failed: no account exists for the supplied email address.");
            return new FailedAuthenticationResponse("Invalid email or password.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            logger.LogInformation("Login failed for User (ID: {UserId}): incorrect password.", user.Id);
            return new FailedAuthenticationResponse("Invalid email or password.");
        }

        var (accessToken, refreshToken, expiresAtUtc) = await tokenService.GenerateTokensAsync(user, cancellationToken);

        logger.LogInformation("Login succeeded for User (ID: {UserId}).", user.Id);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAtUtc: expiresAtUtc
        );
    }
}
