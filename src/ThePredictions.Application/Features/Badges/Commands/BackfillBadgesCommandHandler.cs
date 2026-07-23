using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Badges.Commands;

public class BackfillBadgesCommandHandler(IBadgeEvaluationRepository evaluationRepository, IMediator mediator)
    : IRequestHandler<BackfillBadgesCommand>
{
    public async Task Handle(BackfillBadgesCommand request, CancellationToken cancellationToken)
    {
        var roundIds = await evaluationRepository.GetCompletedRoundIdsAsync(cancellationToken);

        foreach (var roundId in roundIds)
            await mediator.Send(new EvaluateBadgesForRoundCommand(roundId), cancellationToken);
    }
}
