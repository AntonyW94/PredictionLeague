using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Tasks.Commands;

public class CleanupExpiredDataCommandHandler : IRequestHandler<CleanupExpiredDataCommand, CleanupResult>
{
    private const int PasswordResetTokenRetentionDays = 30;

    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly ILogger<CleanupExpiredDataCommandHandler> _logger;

    public CleanupExpiredDataCommandHandler(
        IPasswordResetTokenRepository passwordResetTokenRepository,
        ILogger<CleanupExpiredDataCommandHandler> logger)
    {
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _logger = logger;
    }

    public async Task<CleanupResult> Handle(CleanupExpiredDataCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled cleanup task");

        // Clean up password reset tokens older than 30 days
        var tokenCutoffDate = DateTime.UtcNow.AddDays(-PasswordResetTokenRetentionDays);
        var tokensDeleted = await _passwordResetTokenRepository.DeleteTokensOlderThanAsync(
            tokenCutoffDate,
            cancellationToken);

        if (tokensDeleted > 0)
        {
            _logger.LogInformation(
                "Deleted {TokensDeleted} password reset tokens older than {CutoffDate:yyyy-MM-dd}",
                tokensDeleted,
                tokenCutoffDate);
        }

        _logger.LogInformation("Scheduled cleanup task completed");

        return new CleanupResult(
            PasswordResetTokensDeleted: tokensDeleted
        );
    }
}
