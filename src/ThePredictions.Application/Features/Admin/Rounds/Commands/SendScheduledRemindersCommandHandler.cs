using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class SendScheduledRemindersCommandHandler(
    IRoundRepository roundRepository,
    IEmailService emailService,
    IReminderService reminderService,
    IEmailDateFormatter dateFormatter,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<SendScheduledRemindersCommandHandler> logger) : IRequestHandler<SendScheduledRemindersCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task Handle(SendScheduledRemindersCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;

        var nextRound = await roundRepository.GetNextRoundForReminderAsync(cancellationToken);
        if (nextRound == null)
        {
            logger.LogInformation("Sending Email Reminders: No Active Round.");
            return;
        }

        var shouldSend = await reminderService.ShouldSendReminderAsync(nextRound, nowUtc, cancellationToken);
        if (!shouldSend)
        {
            logger.LogInformation("Sending Email Reminders: Active Round Not Due.");
            return;
        }

        logger.LogInformation("Sending Email Reminders: Sending for Round (ID: {RoundId})", nextRound.Id);

        var usersToChase = await reminderService.GetUsersMissingPredictionsAsync(nextRound.Id, nowUtc, cancellationToken);
        if (!usersToChase.Any())
        {
            logger.LogInformation("Sending Email Reminders: No Users to Chase for Round (ID: {RoundId})", nextRound.Id);
            return;
        }

        var templateId = _brevoSettings.Templates?.PredictionsMissing;
        if (!templateId.HasValue || templateId.Value == 0)
        {
            logger.LogError("Sending Email Reminders: Email Template ID Not Configured.");
            return;
        }

        var baseUrl = _siteSettings.ResolvedBaseUrl;
        var predictionsUrl = $"{baseUrl}/predictions/{nextRound.Id}";

        foreach (var user in usersToChase)
        {
            var timeRemaining = user.DeadlineUtc - nowUtc;

            var parameters = new
            {
                FIRST_NAME = user.FirstName,
                ROUND_NAME = user.RoundName,
                DEADLINE = dateFormatter.FormatDeadline(user.DeadlineUtc),
                PREDICTIONS_URL = predictionsUrl,
                URGENCY = ReminderUrgencyFormatter.GetUrgencyTier(timeRemaining),
                TIME_REMAINING = ReminderUrgencyFormatter.FormatTimeRemaining(timeRemaining)
            };
            await emailService.SendTemplatedEmailAsync(user.Email, templateId.Value, parameters);

            logger.LogInformation("Sent chase notification for Round (ID: {RoundId}) to User (ID: {UserId})", nextRound.Id, user.UserId);
        }

        nextRound.UpdateLastReminderSent(dateTimeProvider);
        await roundRepository.UpdateLastReminderSentAsync(nextRound, cancellationToken);

        logger.LogInformation("Sending Email Reminders: Successfully Sent {Count} Reminders and Updated LastReminderSent for Round (ID: {RoundId})", usersToChase.Count, nextRound.Id);
    }
}