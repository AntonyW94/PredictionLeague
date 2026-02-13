using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class PublishUpcomingRoundsCommandHandler : IRequestHandler<PublishUpcomingRoundsCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<PublishUpcomingRoundsCommandHandler> _logger;

    public PublishUpcomingRoundsCommandHandler(IRoundRepository roundRepository, IDateTimeProvider dateTimeProvider, ILogger<PublishUpcomingRoundsCommandHandler> logger)
    {
        _roundRepository = roundRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task Handle(PublishUpcomingRoundsCommand request, CancellationToken cancellationToken)
    {
        var fourWeeksFromNowUtc = _dateTimeProvider.UtcNow.AddDays(28);

        await PublishDraftRoundsAsync(fourWeeksFromNowUtc, cancellationToken);
        await UnpublishDistantRoundsAsync(fourWeeksFromNowUtc, cancellationToken);
    }

    private async Task PublishDraftRoundsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var roundsToPublish = await _roundRepository.GetDraftRoundsStartingBeforeAsync(cutoffUtc, cancellationToken);

        if (!roundsToPublish.Any())
            return;

        foreach (var round in roundsToPublish.Values)
        {
            round.UpdateStatus(RoundStatus.Published, _dateTimeProvider);
            await _roundRepository.UpdateAsync(round, cancellationToken);
            _logger.LogInformation("Published Round (Number: {RoundNumber}, ID: {RoundId})", round.RoundNumber, round.Id);
        }

        _logger.LogInformation("Successfully published Rounds (Count: {Count})", roundsToPublish.Count);
    }

    private async Task UnpublishDistantRoundsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var roundsToUnpublish = await _roundRepository.GetPublishedRoundsStartingAfterAsync(cutoffUtc, cancellationToken);

        if (!roundsToUnpublish.Any())
            return;

        foreach (var round in roundsToUnpublish.Values)
        {
            round.UpdateStatus(RoundStatus.Draft);
            await _roundRepository.UpdateAsync(round, cancellationToken);
            _logger.LogInformation("Unpublished Round (Number: {RoundNumber}, ID: {RoundId}) — start date moved beyond 28-day window", round.RoundNumber, round.Id);
        }

        _logger.LogInformation("Successfully unpublished Rounds (Count: {Count})", roundsToUnpublish.Count);
    }
}
