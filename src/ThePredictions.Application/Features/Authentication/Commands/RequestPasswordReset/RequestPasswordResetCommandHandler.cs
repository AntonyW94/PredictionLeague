using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Authentication.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandler(
    IUserManager userManager,
    IPasswordResetTokenRepository tokenRepository,
    IEmailService emailService,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<RequestPasswordResetCommandHandler> logger)
    : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    private const int MaxRequestsPerHour = 3;

    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            // Security: Don't reveal that email doesn't exist
            logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return Unit.Value;
        }

        // Check rate limit (3 requests per hour per user)
        var recentRequestCount = await tokenRepository.CountByUserIdSinceAsync(
            user.Id,
            dateTimeProvider.UtcNow.AddHours(-1),
            cancellationToken);

        if (recentRequestCount >= MaxRequestsPerHour)
        {
            // Rate limited - still return success to prevent enumeration
            logger.LogWarning("Password reset rate limit exceeded for User (ID: {UserId})", user.Id);
            return Unit.Value;
        }

        var hasPassword = await userManager.HasPasswordAsync(user);

        if (hasPassword)
        {
            await SendPasswordResetEmailAsync(user, cancellationToken);
        }
        else
        {
            await SendGoogleUserEmailAsync(user);
        }

        return Unit.Value;
    }

    private async Task SendPasswordResetEmailAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        // Create and store the token
        var tokenString = GenerateUrlSafeToken();
        var resetToken = PasswordResetToken.Create(tokenString, user.Id, dateTimeProvider);
        await tokenRepository.CreateAsync(resetToken, cancellationToken);

        // Build the reset link from configured site settings, never a request header
        // (attacker-controllable). No email in the URL for security.
        var resetLink = $"{_siteSettings.ResolvedBaseUrl}/authentication/reset-password?token={resetToken.Token}";

        var templateId = _brevoSettings.Templates?.PasswordReset
            ?? throw new InvalidOperationException("PasswordReset email template ID is not configured");

        await emailService.SendTemplatedEmailAsync(
            user.Email!,
            templateId,
            new
            {
                FIRST_NAME = user.FirstName,
                RESET_LINK = resetLink
            });

        logger.LogInformation("Password reset email sent to User (ID: {UserId})", user.Id);
    }

    private async Task SendGoogleUserEmailAsync(ApplicationUser user)
    {
        // Link base comes from configured site settings, never a request header.
        var loginLink = $"{_siteSettings.ResolvedBaseUrl}/authentication/login";

        var templateId = _brevoSettings.Templates?.PasswordResetGoogleUser
            ?? throw new InvalidOperationException("PasswordResetGoogleUser email template ID is not configured");

        await emailService.SendTemplatedEmailAsync(
            user.Email!,
            templateId,
            new
            {
                FIRST_NAME = user.FirstName,
                LOGIN_LINK = loginLink
            });

        logger.LogInformation("Google sign-in reminder email sent to User (ID: {UserId})", user.Id);
    }

    private static string GenerateUrlSafeToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
