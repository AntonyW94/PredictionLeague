using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetPrizePreviewQueryHandler(
    IPrizeEvaluationInputsReader inputsReader,
    IPrizeEvaluator evaluator,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetPrizePreviewQuery, PrizePreviewDto>
{
    public async Task<PrizePreviewDto> Handle(GetPrizePreviewQuery request, CancellationToken cancellationToken)
    {
        var inputs = await inputsReader.LoadAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, inputs, "League");

        // Private leagues: gate the preview by the entry code the joiner already holds.
        if (inputs.IsPrivate && !string.Equals(inputs.EntryCode, request.EntryCode, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A valid entry code is required to preview this league.");

        return PrizePreviewComposer.Compose(inputs, evaluator, dateTimeProvider);
    }
}
