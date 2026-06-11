using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

public class SendLeagueWelcomeEmailsCommandHandler(
    IMediator mediator,
    IEmailService emailService,
    ILeagueWelcomeNotificationRepository welcomeNotificationRepository,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<SendLeagueWelcomeEmailsCommandHandler> logger) : IRequestHandler<SendLeagueWelcomeEmailsCommand, SendLeagueWelcomeEmailsResult>
{
    /// <summary>
    /// Only leagues whose deadline passed this recently are welcomed, so enabling the scheduled
    /// task never back-fills historic leagues.
    /// </summary>
    private const int DeadlineWindowDays = 7;

    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task<SendLeagueWelcomeEmailsResult> Handle(SendLeagueWelcomeEmailsCommand request, CancellationToken cancellationToken)
    {
        var templateId = _brevoSettings.Templates?.LeagueWelcome;
        if (!templateId.HasValue || templateId.Value == 0)
        {
            logger.LogError("League Welcome: Email template ID not configured.");
            return new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 0, EmailsSent: 0);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        var leagues = await mediator.Send(new GetLeagueWelcomeBatchQuery(nowUtc, nowUtc.AddDays(-DeadlineWindowDays)), cancellationToken);

        if (leagues.Count == 0)
            return new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 0, EmailsSent: 0);

        var baseUrl = string.IsNullOrWhiteSpace(_siteSettings.BaseUrl)
            ? "https://www.thepredictions.co.uk"
            : _siteSettings.BaseUrl.TrimEnd('/');

        var emailsSent = 0;

        foreach (var league in leagues)
        {
            var prizeLines = LeagueWelcomeEmailFormatter.PrizeLines(league);
            var boostLines = LeagueWelcomeEmailFormatter.BoostLines(league);
            var hasPrizes = league.HasPrizes && prizeLines.Count > 0;

            var sentLog = new List<LeagueWelcomeNotification>();

            foreach (var recipient in league.Recipients)
            {
                var parameters = new
                {
                    FIRST_NAME = recipient.FirstName,
                    LEAGUE_NAME = league.LeagueName,
                    SEASON_NAME = league.SeasonName,
                    MEMBER_COUNT = league.MemberCount,
                    HAS_PRIZES = hasPrizes,
                    PRIZE_POT = LeagueWelcomeEmailFormatter.PrizePot(league),
                    PRIZES = prizeLines.Select(line => new
                    {
                        PRIZE_TITLE = line.Title,
                        PRIZE_VALUE = line.Value
                    }).ToList(),
                    HAS_BOOSTS = boostLines.Count > 0,
                    BOOSTS = boostLines.Select(line => new
                    {
                        BOOST_NAME = line.Name,
                        BOOST_DESCRIPTION = line.Description,
                        BOOST_USAGE = line.Usage
                    }).ToList(),
                    LEAGUE_URL = $"{baseUrl}/leagues/{league.LeagueId}/dashboard"
                };

                await emailService.SendTemplatedEmailAsync(recipient.Email, templateId.Value, parameters);
                emailsSent++;

                sentLog.Add(LeagueWelcomeNotification.Create(league.LeagueId, recipient.UserId, dateTimeProvider));
            }

            if (sentLog.Count > 0)
                await welcomeNotificationRepository.AddNotificationsAsync(sentLog, cancellationToken);

            logger.LogInformation("League Welcome: Sent {Count} welcome emails for League (ID: {LeagueId})", sentLog.Count, league.LeagueId);
        }

        return new SendLeagueWelcomeEmailsResult(LeaguesProcessed: leagues.Count, EmailsSent: emailsSent);
    }
}
