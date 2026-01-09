using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class PublishUpcomingRoundsCommandHandler : IRequestHandler<PublishUpcomingRoundsCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ILogger<PublishUpcomingRoundsCommandHandler> _logger;

    public PublishUpcomingRoundsCommandHandler(IRoundRepository roundRepository, ILogger<PublishUpcomingRoundsCommandHandler> logger)
    {
        _roundRepository = roundRepository;
        _logger = logger;
    }

    public async Task Handle(PublishUpcomingRoundsCommand request, CancellationToken cancellationToken)
    {
        var fourWeeksFromNowUtc = DateTime.UtcNow.AddDays(28);
        var roundsToPublish = await _roundRepository.GetDraftRoundsStartingBeforeAsync(fourWeeksFromNowUtc, cancellationToken);

        if (!roundsToPublish.Any())
        {
            _logger.LogInformation("No draft rounds found starting before{FourWeeksFromNow} to publish.", fourWeeksFromNowUtc);
            return;
        }

        foreach (var round in roundsToPublish.Values)
        {
            round.UpdateStatus(RoundStatus.Published);

            await _roundRepository.UpdateAsync(round, cancellationToken);

            _logger.LogInformation("Published Round {RoundNumber} (ID: {RoundId})", round.RoundNumber, round.Id);
        }

        _logger.LogInformation("Successfully published {Count} rounds.", roundsToPublish.Count());
    }
}