using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <inheritdoc cref="UpdateMatchResultsCommand"/>
public class UpdateMatchResultsCommandHandler(IMediator mediator) : IRequestHandler<UpdateMatchResultsCommand>
{
    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        var outcome = await mediator.Send(new ScoreMatchResultsCommand(request.RoundId, request.Matches), cancellationToken);

        // Sent only after the scoring command's transaction has committed, which is the whole point of the
        // split: the settlement work reads the rows that were just written, and holding write locks on them
        // while sending a season's worth of email is what made an unrelated dashboard read wait 615ms.
        if (outcome.RoundFinished)
            await mediator.Send(new CompleteRoundCommand(request.RoundId), cancellationToken);
    }
}
