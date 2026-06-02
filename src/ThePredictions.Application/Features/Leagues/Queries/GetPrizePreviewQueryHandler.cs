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

        var deadlinePassed = inputs.EntryDeadlineUtc < dateTimeProvider.UtcNow;
        var hasPrizes = inputs.HasScheme && (inputs.EntryCost > 0 || inputs.AdminTopUpPounds > 0);
        var currentPot = inputs.EntryCost * inputs.EntrantCount + inputs.AdminTopUpPounds;

        var breakdown = new PrizeBreakdownDto { EntrantCount = inputs.EntrantCount };
        var attribution = new List<string>();
        var projectedPot = currentPot;

        if (hasPrizes)
        {
            var current = evaluator.Evaluate(inputs.ToEvaluationRequest(inputs.EntrantCount));

            if (deadlinePassed)
            {
                // After the deadline the pot is final - show the current breakdown, no joining delta.
                breakdown = current;
            }
            else
            {
                var projected = evaluator.Evaluate(inputs.ToEvaluationRequest(inputs.EntrantCount + 1));
                (breakdown, attribution) = PrizePreviewBuilder.Build(current, projected, inputs.EntryCost);
                projectedPot = projected.Pot;
            }
        }

        return new PrizePreviewDto
        {
            LeagueName = inputs.LeagueName,
            AdministratorName = inputs.AdministratorName,
            EntrantCount = inputs.EntrantCount,
            EntryCost = inputs.EntryCost,
            CurrentPrizePot = currentPot,
            ProjectedPrizePot = projectedPot,
            EntryDeadlineUtc = inputs.EntryDeadlineUtc,
            DeadlinePassed = deadlinePassed,
            HasPrizes = hasPrizes,
            Breakdown = breakdown,
            Attribution = attribution
        };
    }
}
