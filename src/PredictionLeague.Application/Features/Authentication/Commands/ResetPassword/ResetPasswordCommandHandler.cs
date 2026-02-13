using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Common;

namespace PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserManager _userManager;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserManager userManager,
        IAuthenticationTokenService tokenService,
        IDateTimeProvider dateTimeProvider,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _userManager = userManager;
        _tokenService = tokenService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<ResetPasswordResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Clean up expired tokens (opportunistic cleanup)
        await _tokenRepository.DeleteExpiredTokensAsync(cancellationToken);

        // Look up the token
        var resetToken = await _tokenRepository.GetByTokenAsync(request.Token, cancellationToken);

        if (resetToken == null)
        {
            _logger.LogWarning("Password reset attempted with non-existent token");
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        if (resetToken.IsExpired(_dateTimeProvider))
        {
            _logger.LogWarning("Password reset attempted with expired token for User (ID: {UserId})", resetToken.UserId);
            await _tokenRepository.DeleteAsync(request.Token, cancellationToken);
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        // Look up the user
        var user = await _userManager.FindByIdAsync(resetToken.UserId);

        if (user == null)
        {
            _logger.LogWarning("Password reset token references non-existent User (ID: {UserId})", resetToken.UserId);
            await _tokenRepository.DeleteAsync(request.Token, cancellationToken);
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        // Reset the password using Identity's password hasher
        var result = await _userManager.ResetPasswordDirectAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Password reset failed for User (ID: {UserId}). Errors: {Errors}",
                user.Id, string.Join(", ", result.Errors));

            // Return the first error (usually password policy violation)
            var errorMessage = result.Errors.FirstOrDefault() ?? "Password reset failed.";
            return new FailedResetPasswordResponse(errorMessage);
        }

        // Delete the used token (and any other tokens for this user)
        await _tokenRepository.DeleteByUserIdAsync(user.Id, cancellationToken);

        _logger.LogInformation("Password successfully reset for User (ID: {UserId})", user.Id);

        // Auto-login: Generate tokens for the user
        var (accessToken, refreshToken, expiresAtUtc) = await _tokenService.GenerateTokensAsync(user, cancellationToken);

        return new SuccessfulResetPasswordResponse(
            AccessToken: accessToken,
            ExpiresAtUtc: expiresAtUtc,
            RefreshTokenForCookie: refreshToken
        );
    }
}
