using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetPrizePreviewByCodeQueryHandler(
    IPrizeEvaluationInputsReader inputsReader,
    IPrizeEvaluator evaluator,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<GetPrizePreviewByCodeQuery, PrizePreviewDto>
{
    public async Task<PrizePreviewDto> Handle(GetPrizePreviewByCodeQuery request, CancellationToken cancellationToken)
    {
        // Loading by the entry code is itself the gate - only a holder of the code can preview.
        var inputs = await inputsReader.LoadByEntryCodeAsync(request.EntryCode, cancellationToken);
        Guard.Against.EntityNotFound(request.EntryCode, inputs, "League");

        // Paid leagues: surface how to pay (bank details / manual fallback). The entry code authorises it.
        LeaguePaymentInfoDto? payment = null;
        if (inputs.EntryCost > 0)
            payment = await mediator.Send(new GetLeaguePaymentInfoQuery(inputs.LeagueId, request.RequestingUserId, request.EntryCode), cancellationToken);

        return PrizePreviewComposer.Compose(inputs, evaluator, dateTimeProvider, payment);
    }
}
