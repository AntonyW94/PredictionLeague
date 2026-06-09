using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class SendPrizeNotificationsCommandHandler(
    IMediator mediator,
    IRoundRepository roundRepository,
    IEmailService emailService,
    IPrizeNotificationRepository prizeNotificationRepository,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<SendPrizeNotificationsCommandHandler> logger) : IRequestHandler<SendPrizeNotificationsCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task Handle(SendPrizeNotificationsCommand request, CancellationToken cancellationToken)
    {
        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
        {
            logger.LogWarning("Prize Won: Round (ID: {RoundId}) not found.", request.RoundId);
            return;
        }

        if (round.Status != RoundStatus.Completed)
        {
            logger.LogInformation("Prize Won: Round (ID: {RoundId}) is not completed; skipping.", round.Id);
            return;
        }

        var templateId = _brevoSettings.Templates?.PrizeWon;
        if (!templateId.HasValue || templateId.Value == 0)
        {
            logger.LogError("Prize Won: Email template ID not configured.");
            return;
        }

        var winners = await mediator.Send(new GetPrizeWinnersForRoundQuery(round.Id), cancellationToken);

        var baseUrl = string.IsNullOrWhiteSpace(_siteSettings.BaseUrl)
            ? "https://www.thepredictions.co.uk"
            : _siteSettings.BaseUrl.TrimEnd('/');

        var sentLog = new List<PrizeNotification>();
        var emailsSent = 0;

        foreach (var winner in winners)
        {
            // Normally only send prizes the winner hasn't been told about; Force re-sends everything.
            var prizesToSend = request.Force
                ? winner.Prizes
                : winner.Prizes.Where(prize => !prize.AlreadyNotified).ToList();

            if (prizesToSend.Count == 0)
                continue;

            var parameters = new
            {
                FIRST_NAME = winner.FirstName,
                ROUND_NAME = winner.RoundName,
                PRIZE_COUNT = prizesToSend.Count,
                PRIZES = prizesToSend.Select(prize => new
                {
                    PRIZE_TITLE = PrizeNotificationFormatter.Title(prize),
                    LEAGUE_NAME = prize.LeagueName,
                    PRIZE_VALUE = PrizeNotificationFormatter.Money(prize.Amount),
                    LEAGUE_URL = $"{baseUrl}/leagues/{prize.LeagueId}/dashboard"
                }).ToList()
            };

            await emailService.SendTemplatedEmailAsync(winner.Email, templateId.Value, parameters);
            emailsSent++;

            // Only log prizes not already recorded, so a forced re-send can't violate the unique key.
            foreach (var prize in prizesToSend.Where(prize => !prize.AlreadyNotified))
            {
                sentLog.Add(PrizeNotification.Create(
                    winner.UserId,
                    prize.LeaguePrizeSettingId,
                    prize.RoundNumber,
                    prize.Month,
                    dateTimeProvider));
            }
        }

        if (sentLog.Count > 0)
            await prizeNotificationRepository.AddNotificationsAsync(sentLog, cancellationToken);

        logger.LogInformation("Prize Won: Sent {Count} emails for Round (ID: {RoundId}).", emailsSent, round.Id);
    }
}
