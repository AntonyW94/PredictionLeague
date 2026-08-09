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
        var templateId = ResolveTemplateId(round, request.RoundId);
        if (templateId is null)
            return;

        var winners = await mediator.Send(new GetPrizeWinnersForRoundQuery(round!.Id), cancellationToken);

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

            await emailService.SendTemplatedEmailAsync(winner.Email, templateId.Value, BuildParameters(winner, prizesToSend));
            emailsSent++;

            sentLog.AddRange(BuildSentLog(winner, prizesToSend));
        }

        if (sentLog.Count > 0)
            await prizeNotificationRepository.AddNotificationsAsync(sentLog, cancellationToken);

        logger.LogInformation("Prize Won: Sent {Count} emails for Round (ID: {RoundId}).", emailsSent, round.Id);
    }

    /// <summary>
    /// The template to send with, or null when there is nothing to do: no such round, a round that
    /// has not finished, or no configured template.
    /// </summary>
    private long? ResolveTemplateId(Round? round, int requestedRoundId)
    {
        if (round is null)
        {
            logger.LogWarning("Prize Won: Round (ID: {RoundId}) not found.", requestedRoundId);
            return null;
        }

        if (round.Status != RoundStatus.Completed)
        {
            logger.LogInformation("Prize Won: Round (ID: {RoundId}) is not completed; skipping.", round.Id);
            return null;
        }

        var templateId = _brevoSettings.Templates?.PrizeWon;
        if (templateId is null or 0)
        {
            logger.LogError("Prize Won: Email template ID not configured.");
            return null;
        }

        return templateId;
    }

    /// <summary>The merge fields for one winner's email, listing every prize being announced.</summary>
    private object BuildParameters(PrizeWinner winner, IReadOnlyList<WonPrize> prizesToSend)
    {
        var baseUrl = _siteSettings.ResolvedBaseUrl;

        return new
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
    }

    /// <summary>
    /// Only prizes not already recorded, so a forced re-send announces everything again without
    /// violating the sent-log's unique key.
    /// </summary>
    private IEnumerable<PrizeNotification> BuildSentLog(PrizeWinner winner, IReadOnlyList<WonPrize> prizesToSend) =>
        prizesToSend
            .Where(prize => !prize.AlreadyNotified)
            .Select(prize => PrizeNotification.Create(
                winner.UserId,
                prize.LeaguePrizeSettingId,
                prize.RoundNumber,
                prize.Month,
                dateTimeProvider));
}
