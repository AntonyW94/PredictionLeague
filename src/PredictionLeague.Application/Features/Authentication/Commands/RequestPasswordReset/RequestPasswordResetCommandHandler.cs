using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    private const int MaxRequestsPerHour = 3;

    private readonly IUserManager _userManager;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailService _emailService;
    private readonly BrevoSettings _brevoSettings;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        IUserManager userManager,
        IPasswordResetTokenRepository tokenRepository,
        IEmailService emailService,
        IOptions<BrevoSettings> brevoSettings,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _userManager = userManager;
        _tokenRepository = tokenRepository;
        _emailService = emailService;
        _brevoSettings = brevoSettings.Value;
        _logger = logger;
    }

    public async Task<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            // Security: Don't reveal that email doesn't exist
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return Unit.Value;
        }

        // Check rate limit (3 requests per hour per user)
        var recentRequestCount = await _tokenRepository.CountByUserIdSinceAsync(
            user.Id,
            DateTime.UtcNow.AddHours(-1),
            cancellationToken);

        if (recentRequestCount >= MaxRequestsPerHour)
        {
            // Rate limited - still return success to prevent enumeration
            _logger.LogWarning("Password reset rate limit exceeded for User (ID: {UserId})", user.Id);
            return Unit.Value;
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);

        if (hasPassword)
        {
            await SendPasswordResetEmailAsync(user, request.ResetUrlBase, cancellationToken);
        }
        else
        {
            await SendGoogleUserEmailAsync(user, request.ResetUrlBase);
        }

        return Unit.Value;
    }

    private async Task SendPasswordResetEmailAsync(
        ApplicationUser user,
        string resetUrlBase,
        CancellationToken cancellationToken)
    {
        // Create and store the token
        var tokenString = GenerateUrlSafeToken();
        var resetToken = PasswordResetToken.Create(tokenString, user.Id);
        await _tokenRepository.CreateAsync(resetToken, cancellationToken);

        // Build the reset link (no email in URL for security)
        var resetLink = $"{resetUrlBase}?token={resetToken.Token}";

        var templateId = _brevoSettings.Templates?.PasswordReset
            ?? throw new InvalidOperationException("PasswordReset email template ID is not configured");

        await _emailService.SendTemplatedEmailAsync(
            user.Email!,
            templateId,
            new
            {
                firstName = user.FirstName,
                resetLink
            });

        _logger.LogInformation("Password reset email sent to User (ID: {UserId})", user.Id);
    }

    private async Task SendGoogleUserEmailAsync(
        ApplicationUser user,
        string resetUrlBase)
    {
        // Extract base URL (remove the reset-password path)
        var baseUrl = resetUrlBase.Replace("/authentication/reset-password", "");
        var loginLink = $"{baseUrl}/authentication/login";

        var templateId = _brevoSettings.Templates?.PasswordResetGoogleUser
            ?? throw new InvalidOperationException("PasswordResetGoogleUser email template ID is not configured");

        await _emailService.SendTemplatedEmailAsync(
            user.Email!,
            templateId,
            new
            {
                firstName = user.FirstName,
                loginLink
            });

        _logger.LogInformation("Google sign-in reminder email sent to User (ID: {UserId})", user.Id);
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
