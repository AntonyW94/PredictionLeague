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

        var emailsSent = 0;

        foreach (var league in leagues)
        {
            emailsSent += await SendForLeagueAsync(league, templateId.Value, cancellationToken);
        }

        return new SendLeagueWelcomeEmailsResult(LeaguesProcessed: leagues.Count, EmailsSent: emailsSent);
    }

    /// <summary>
    /// Welcomes everyone waiting on this league and records who was told, so a later run does not
    /// welcome them twice.
    /// </summary>
    private async Task<int> SendForLeagueAsync(LeagueWelcomeLeague league, long templateId, CancellationToken cancellationToken)
    {
        var sentLog = new List<LeagueWelcomeNotification>();

        foreach (var recipient in league.Recipients)
        {
            await emailService.SendTemplatedEmailAsync(recipient.Email, templateId, BuildParameters(league, recipient));
            sentLog.Add(LeagueWelcomeNotification.Create(league.LeagueId, recipient.UserId, dateTimeProvider));
        }

        if (sentLog.Count > 0)
            await welcomeNotificationRepository.AddNotificationsAsync(sentLog, cancellationToken);

        logger.LogInformation("League Welcome: Sent {Count} welcome emails for League (ID: {LeagueId})", sentLog.Count, league.LeagueId);

        return sentLog.Count;
    }

    /// <summary>The merge fields for one recipient: the league's prizes, its boosts and a way in.</summary>
    private object BuildParameters(LeagueWelcomeLeague league, LeagueWelcomeRecipient recipient)
    {
        var baseUrl = _siteSettings.ResolvedBaseUrl;
        var prizeSections = LeagueWelcomeEmailFormatter.PrizeSections(league);
        var boostLines = LeagueWelcomeEmailFormatter.BoostLines(league);

        return new
        {
            FIRST_NAME = recipient.FirstName,
            LEAGUE_NAME = league.LeagueName,
            SEASON_NAME = league.SeasonName,
            MEMBER_COUNT = league.MemberCount,
            HAS_PRIZES = league.HasPrizes && prizeSections.Count > 0,
            PRIZE_POT = LeagueWelcomeEmailFormatter.PrizePot(league),
            PRIZE_SECTIONS = prizeSections.Select(section => new
            {
                SECTION_TITLE = section.Title,
                PRIZES = section.Prizes.Select(line => new
                {
                    PRIZE_TITLE = line.Title,
                    PRIZE_VALUE = line.Value,
                    IS_TOP = line.IsTop
                }).ToList()
            }).ToList(),
            HAS_BOOSTS = boostLines.Count > 0,
            BOOSTS = boostLines.Select(line => new
            {
                BOOST_NAME = line.Name,
                BOOST_DESCRIPTION = line.Description,
                BOOST_USAGE = line.Usage,
                BOOST_IMAGE_URL = AbsoluteImageUrl(line.ImageUrl)
            }).ToList(),
            LEAGUE_URL = $"{baseUrl}/leagues/{league.LeagueId}/dashboard"
        };
    }

    /// <summary>
    /// Static images in emails are always served from the canonical production site (matching the
    /// hard-coded header logo in every template) - dev-hosted images break whenever dev redeploys,
    /// and an email outlives any deployment.
    /// </summary>
    private const string CanonicalImageBaseUrl = "https://www.thepredictions.co.uk";

    private static string AbsoluteImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return string.Empty;

        return imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? imageUrl
            : $"{CanonicalImageBaseUrl}{imageUrl}";
    }
}
