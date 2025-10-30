using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class SendScheduledRemindersCommandHandler : IRequestHandler<SendScheduledRemindersCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IEmailService _emailService;
    private readonly IReminderService _reminderService;
    private readonly BrevoSettings _brevoSettings;
    private readonly ILogger<SendScheduledRemindersCommandHandler> _logger;

    public SendScheduledRemindersCommandHandler(
        IRoundRepository roundRepository,
        IEmailService emailService,
        IReminderService reminderService,
        IOptions<BrevoSettings> brevoSettings,
        ILogger<SendScheduledRemindersCommandHandler> logger)
    {
        _roundRepository = roundRepository;
        _emailService = emailService;
        _reminderService = reminderService;
        _brevoSettings = brevoSettings.Value;
        _logger = logger;
    }

    public async Task Handle(SendScheduledRemindersCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        var nextRound = await _roundRepository.GetNextRoundForReminderAsync(cancellationToken);
        if (nextRound == null)
        {
            _logger.LogInformation("Sending Email Reminders: No Active Round.");
            return;
        }

        var shouldSend = await _reminderService.ShouldSendReminderAsync(nextRound, now, cancellationToken);
        if (!shouldSend)
        {
            _logger.LogInformation("Sending Email Reminders: Active Round Not Due.");
            return;
        }

        _logger.LogInformation("Sending Email Reminders: Sending for Round (ID: {RoundId})", nextRound.Id);

        var usersToChase = await _reminderService.GetUsersMissingPredictionsAsync(nextRound.Id, cancellationToken);
        if (!usersToChase.Any())
        {
            _logger.LogInformation("Sending Email Reminders: No Users to Chase for Round (ID: {RoundId})", nextRound.Id);
            return;
        }

        var templateId = _brevoSettings.Templates?.PredictionsMissing;
        if (!templateId.HasValue || templateId.Value == 0)
        {
            _logger.LogError("Sending Email Reminders: Email Template ID Not Configured.");
            return;
        }

        foreach (var user in usersToChase)
        {
            var parameters = new
            {
                FIRST_NAME = user.FirstName,
                ROUND_NAME = user.RoundName,
                DEADLINE = user.Deadline.ToString("dddd, dd MMMM yyyy 'at' HH:mm")
            };
            await _emailService.SendTemplatedEmailAsync(user.Email, templateId.Value, parameters);

            _logger.LogInformation("Sending Email Reminders: Sent {Email} Chase Notification for Round (ID: {RoundId})", user.Email, nextRound.Id);
        }

        nextRound.UpdateLastReminderSent();
        await _roundRepository.UpdateLastReminderSentAsync(nextRound, cancellationToken);

        _logger.LogInformation("Sending Email Reminders: Successfully Sent {Count} Reminders and Updated LastReminderSent for Round (ID: {RoundId})", usersToChase.Count, nextRound.Id);
    }
}