using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Commands;

public class SendTestEmailCommandHandler(
    IUserManager userManager,
    IEmailService emailService,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    ILogger<SendTestEmailCommandHandler> logger)
    : IRequestHandler<SendTestEmailCommand, SendTestEmailResultDto>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task<SendTestEmailResultDto> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        var caller = await userManager.FindByIdAsync(request.CallerUserId);

        if (caller is null || string.IsNullOrWhiteSpace(caller.Email))
        {
            logger.LogWarning("Test email requested but caller (ID: {UserId}) has no email address", request.CallerUserId);
            return new SendTestEmailResultDto(false, null, "Could not determine your email address to send the test to.", string.Empty);
        }

        var parameters = BuildParameters(request);

        var result = await emailService.SendTestTemplatedEmailAsync(caller.Email, request.TemplateId, parameters);

        logger.LogInformation(
            "Test email for template (ID: {TemplateId}) sent to {Email}: success={Success}",
            request.TemplateId,
            caller.Email,
            result.Success);

        return new SendTestEmailResultDto(result.Success, result.MessageId, result.Error, caller.Email);
    }

    // The test tool discovers scalar {{ params.X }} tags only, so loop-driven sections stay empty.
    // For the round-results digest, inject a representative BADGES + LEAGUES sample so the whole
    // email (including the badges section) can be previewed from the admin test tool.
    private object BuildParameters(SendTestEmailCommand request)
    {
        if (request.TemplateId != _brevoSettings.Templates?.RoundResultsDigest)
            return request.Parameters;

        var enriched = new Dictionary<string, object>(request.Parameters.Count + 2);
        foreach (var pair in request.Parameters)
            enriched[pair.Key] = pair.Value;

        var baseUrl = _siteSettings.ResolvedBaseUrl.TrimEnd('/');

        enriched["BADGES"] = new[]
        {
            new { NAME = "First Blood", ICON_URL = $"{baseUrl}/api/badges/first-blood.png" },
            new { NAME = "Round Winner", ICON_URL = $"{baseUrl}/api/badges/round-winner.png" },
            new { NAME = "Sharpshooter", ICON_URL = $"{baseUrl}/api/badges/sharpshooter-3.png" }
        };

        enriched["LEAGUES"] = new[]
        {
            new
            {
                LEAGUE_NAME = "Test League",
                POINTS = 18,
                POSITION = "2nd",
                MOVEMENT_ARROW = "▲",
                MOVEMENT_COLOUR = "#00824A",
                MOVEMENT_COUNT = "1",
                TOP_SCORER = "Sarah J",
                TOP_SCORER_POINTS = 24,
                LEAGUE_URL = $"{baseUrl}/leagues"
            }
        };

        return enriched;
    }
}
