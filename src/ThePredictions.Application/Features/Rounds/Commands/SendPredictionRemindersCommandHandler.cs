using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Rounds;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Rounds.Commands;

public class SendPredictionRemindersCommandHandler(
    IRoundRepository roundRepository,
    IReminderService reminderService,
    IPredictionReminderNotificationRepository reminderNotificationRepository,
    ILeagueMembershipService membershipService,
    IEmailService emailService,
    IEmailDateFormatter dateFormatter,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<SendPredictionRemindersCommandHandler> logger) : IRequestHandler<SendPredictionRemindersCommand, SendPredictionRemindersResultDto>
{
    // A player is not re-reminded about the same round within this window, no matter who triggers the
    // send. Predictions are per season, so a player in several leagues would otherwise be nudged once
    // per league owner.
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromHours(6);

    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task<SendPredictionRemindersResultDto> Handle(SendPredictionRemindersCommand request, CancellationToken cancellationToken)
    {
        await AuthoriseAsync(request, cancellationToken);

        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round == null)
            throw new KeyNotFoundException($"Round with ID {request.RoundId} not found.");

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc >= round.DeadlineUtc)
            throw new InvalidOperationException("The prediction deadline for this round has passed, so reminders can no longer be sent.");

        var requestedUserIds = request.UserIds.Distinct().ToList();
        if (requestedUserIds.Count == 0)
            return new SendPredictionRemindersResultDto(0, 0, 0);

        var templateId = _brevoSettings.Templates?.PredictionsMissing;
        if (!templateId.HasValue || templateId.Value == 0)
            throw new InvalidOperationException("The predictions-missing email template is not configured.");

        // Only chase players who still have at least one actionable missing fixture (the reminder
        // service applies the same "predictable match" rule as the completion view).
        var usersStillMissing = (await reminderService.GetUsersMissingPredictionsAsync(request.RoundId, nowUtc, cancellationToken))
            .Where(u => requestedUserIds.Contains(u.UserId))
            .ToList();

        var skippedNoLongerMissing = requestedUserIds.Count - usersStillMissing.Count;

        var lastReminded = await reminderNotificationRepository.GetLastRemindedUtcAsync(
            request.RoundId,
            usersStillMissing.Select(u => u.UserId),
            cancellationToken);

        var throttleCutoff = nowUtc - ThrottleWindow;
        var baseUrl = string.IsNullOrWhiteSpace(_siteSettings.BaseUrl)
            ? "https://www.thepredictions.co.uk"
            : _siteSettings.BaseUrl.TrimEnd('/');
        var predictionsUrl = $"{baseUrl}/predictions/{request.RoundId}";

        var sentCount = 0;
        var skippedRecentlyReminded = 0;

        foreach (var user in usersStillMissing)
        {
            if (lastReminded.TryGetValue(user.UserId, out var remindedAt) && remindedAt > throttleCutoff)
            {
                skippedRecentlyReminded++;
                continue;
            }

            var parameters = new
            {
                FIRST_NAME = user.FirstName,
                ROUND_NAME = user.RoundName,
                DEADLINE = dateFormatter.FormatDeadline(user.DeadlineUtc),
                PREDICTIONS_URL = predictionsUrl
            };
            await emailService.SendTemplatedEmailAsync(user.Email, templateId.Value, parameters);

            var notification = PredictionReminderNotification.Create(request.RoundId, user.UserId, request.CurrentUserId, dateTimeProvider);
            await reminderNotificationRepository.UpsertAsync(notification, cancellationToken);

            sentCount++;
            logger.LogInformation("Sent ad-hoc prediction reminder for Round (ID: {RoundId}) to User (ID: {UserId})", request.RoundId, user.UserId);
        }

        logger.LogInformation(
            "Ad-hoc prediction reminders for Round (ID: {RoundId}): sent {Sent}, skipped {SkippedRecent} recently reminded, skipped {SkippedComplete} no longer missing",
            request.RoundId, sentCount, skippedRecentlyReminded, skippedNoLongerMissing);

        return new SendPredictionRemindersResultDto(sentCount, skippedRecentlyReminded, skippedNoLongerMissing);
    }

    private async Task AuthoriseAsync(SendPredictionRemindersCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId == null)
        {
            if (!request.IsSiteAdmin)
                throw new UnauthorizedAccessException("Only an administrator can send reminders across all leagues.");

            return;
        }

        if (!request.IsSiteAdmin)
            await membershipService.EnsureLeagueAdministratorAsync(request.LeagueId.Value, request.CurrentUserId, cancellationToken);
    }
}
