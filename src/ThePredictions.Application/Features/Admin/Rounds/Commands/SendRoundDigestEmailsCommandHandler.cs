using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class SendRoundDigestEmailsCommandHandler(
    IMediator mediator,
    IRoundRepository roundRepository,
    IEmailService emailService,
    IEmailDateFormatter dateFormatter,
    IOptions<BrevoSettings> brevoSettings,
    IOptions<SiteSettings> siteSettings,
    IDateTimeProvider dateTimeProvider,
    ILogger<SendRoundDigestEmailsCommandHandler> logger) : IRequestHandler<SendRoundDigestEmailsCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task Handle(SendRoundDigestEmailsCommand request, CancellationToken cancellationToken)
    {
        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
        {
            logger.LogWarning("Round Results Digest: Round (ID: {RoundId}) not found.", request.RoundId);
            return;
        }

        if (round.Status != RoundStatus.Completed)
        {
            logger.LogInformation("Round Results Digest: Round (ID: {RoundId}) is not completed; skipping.", round.Id);
            return;
        }

        if (round.ResultsDigestSentUtc is not null && !request.Force)
        {
            logger.LogInformation("Round Results Digest: Already sent for Round (ID: {RoundId}); skipping.", round.Id);
            return;
        }

        var templateId = _brevoSettings.Templates?.RoundResultsDigest;
        if (!templateId.HasValue || templateId.Value == 0)
        {
            logger.LogError("Round Results Digest: Email template ID not configured.");
            return;
        }

        var digests = await mediator.Send(new GetRoundDigestQuery(round.Id), cancellationToken);

        var baseUrl = string.IsNullOrWhiteSpace(_siteSettings.BaseUrl)
            ? "https://www.thepredictions.co.uk"
            : _siteSettings.BaseUrl.TrimEnd('/');
        var resultsUrl = $"{baseUrl}/dashboard";

        foreach (var digest in digests)
        {
            var parameters = new
            {
                FIRST_NAME = digest.FirstName,
                ROUND_NAME = digest.RoundName,
                CORRECT_RESULTS = digest.CorrectResultCount,
                EXACT_SCORES = digest.ExactScoreCount,
                RESULTS_URL = resultsUrl,
                NEXT_ROUND_NAME = digest.NextRoundName ?? string.Empty,
                NEXT_ROUND_OPENS = digest.NextRoundStartUtc.HasValue ? dateFormatter.FormatDeadline(digest.NextRoundStartUtc.Value) : string.Empty,
                NEXT_ROUND_DEADLINE = digest.NextRoundDeadlineUtc.HasValue ? dateFormatter.FormatDeadline(digest.NextRoundDeadlineUtc.Value) : string.Empty,
                LEAGUES = digest.Leagues.Select(league => new
                {
                    LEAGUE_NAME = league.LeagueName,
                    POINTS = league.Points,
                    POSITION = DigestEmailFormatter.Ordinal(league.Position),
                    MOVEMENT = DigestEmailFormatter.PositionMovement(league.PositionDelta),
                    TOP_SCORER = league.TopScorerName ?? string.Empty,
                    TOP_SCORER_POINTS = league.TopScorerPoints ?? 0
                }).ToList()
            };

            await emailService.SendTemplatedEmailAsync(digest.Email, templateId.Value, parameters);
        }

        round.MarkResultsDigestSent(dateTimeProvider);
        await roundRepository.UpdateResultsDigestSentAsync(round, cancellationToken);

        logger.LogInformation("Round Results Digest: Sent {Count} emails for Round (ID: {RoundId}).", digests.Count, round.Id);
    }
}
