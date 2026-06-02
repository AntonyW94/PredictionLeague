using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class PublishUpcomingRoundsCommandHandler(IRoundRepository roundRepository, IDateTimeProvider dateTimeProvider, ILogger<PublishUpcomingRoundsCommandHandler> logger) : IRequestHandler<PublishUpcomingRoundsCommand>
{
    public async Task Handle(PublishUpcomingRoundsCommand request, CancellationToken cancellationToken)
    {
        var sixWeeksFromNowUtc = dateTimeProvider.UtcNow.AddDays(42);

        await PublishDraftRoundsAsync(sixWeeksFromNowUtc, cancellationToken);
        await UnpublishDistantRoundsAsync(sixWeeksFromNowUtc, cancellationToken);
        await UnpublishRoundsWithoutConfirmedFixturesAsync(cancellationToken);
    }

    private async Task UnpublishRoundsWithoutConfirmedFixturesAsync(CancellationToken cancellationToken)
    {
        // A round is published once any of its fixtures has confirmed teams. If sync
        // later removes those fixtures (e.g. the API drops a knockout draw), the round
        // can be left showing only TBD placeholders. Revert it to Draft so it isn't
        // shown to players until its teams are known again. InProgress/Completed
        // rounds are not affected (this only looks at Published rounds).
        var publishedRounds = await roundRepository.GetPublishedRoundsAsync(cancellationToken);

        if (!publishedRounds.Any())
            return;

        var unpublishedCount = 0;

        foreach (var round in publishedRounds.Values)
        {
            if (round.HasConfirmedFixtures)
                continue;

            round.UpdateStatus(RoundStatus.Draft, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);
            logger.LogInformation("Unpublished Round (Number: {RoundNumber}, ID: {RoundId}) — no fixtures with confirmed teams", round.RoundNumber, round.Id);
            unpublishedCount++;
        }

        if (unpublishedCount > 0)
            logger.LogInformation("Successfully unpublished Rounds with no confirmed fixtures (Count: {Count})", unpublishedCount);
    }

    private async Task PublishDraftRoundsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var roundsToPublish = await roundRepository.GetDraftRoundsStartingBeforeAsync(cutoffUtc, cancellationToken);

        if (!roundsToPublish.Any())
            return;

        var publishedCount = 0;

        foreach (var round in roundsToPublish.Values)
        {
            if (!round.HasConfirmedFixtures)
            {
                logger.LogInformation("Skipped publishing Round (Number: {RoundNumber}, ID: {RoundId}) - no fixtures with confirmed teams yet", round.RoundNumber, round.Id);
                continue;
            }

            round.UpdateStatus(RoundStatus.Published, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);
            logger.LogInformation("Published Round (Number: {RoundNumber}, ID: {RoundId})", round.RoundNumber, round.Id);
            publishedCount++;
        }

        logger.LogInformation("Successfully published Rounds (Count: {Count})", publishedCount);
    }

    private async Task UnpublishDistantRoundsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var roundsToUnpublish = await roundRepository.GetPublishedRoundsStartingAfterAsync(cutoffUtc, cancellationToken);

        if (!roundsToUnpublish.Any())
            return;

        foreach (var round in roundsToUnpublish.Values)
        {
            round.UpdateStatus(RoundStatus.Draft, dateTimeProvider);
            await roundRepository.UpdateAsync(round, cancellationToken);
            logger.LogInformation("Unpublished Round (Number: {RoundNumber}, ID: {RoundId}) — start date moved beyond 6-week window", round.RoundNumber, round.Id);
        }

        logger.LogInformation("Successfully unpublished Rounds (Count: {Count})", roundsToUnpublish.Count);
    }
}
