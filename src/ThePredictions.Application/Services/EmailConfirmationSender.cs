using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public class EmailConfirmationSender(
    IEmailConfirmationTokenRepository tokenRepository,
    IEmailService emailService,
    IOptions<BrevoSettings> brevoSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<EmailConfirmationSender> logger)
    : IEmailConfirmationSender
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;

    public async Task SendAsync(ApplicationUser user, string confirmUrlBase, CancellationToken cancellationToken)
    {
        // One active token per user.
        await tokenRepository.DeleteByUserIdAsync(user.Id, cancellationToken);

        var tokenString = GenerateUrlSafeToken();
        var token = EmailConfirmationToken.Create(tokenString, user.Id, dateTimeProvider);
        await tokenRepository.CreateAsync(token, cancellationToken);

        var templateId = _brevoSettings.Templates?.EmailConfirmation ?? 0;
        if (templateId <= 0)
        {
            // Template not configured yet - the token is stored so a later resend can deliver it.
            logger.LogWarning("EmailConfirmation template ID is not configured; skipping send for User (ID: {UserId})", user.Id);
            return;
        }

        var confirmLink = $"{confirmUrlBase}?token={token.Token}";

        try
        {
            await emailService.SendTemplatedEmailAsync(
                user.Email!,
                templateId,
                new
                {
                    FIRST_NAME = user.FirstName,
                    CONFIRM_LINK = confirmLink
                });

            logger.LogInformation("Email confirmation sent to User (ID: {UserId})", user.Id);
        }
        catch (Exception ex)
        {
            // Never block registration on email delivery - the user can resend later.
            logger.LogError(ex, "Failed to send email confirmation to User (ID: {UserId})", user.Id);
        }
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
