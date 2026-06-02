using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Builds the prospective-member <see cref="PrizePreviewDto"/> from loaded evaluation inputs:
/// headline facts, the projected breakdown if they join, and the attributed "+£x" effect of their
/// own entry. Shared by the by-id and by-code preview query handlers.
/// </summary>
public static class PrizePreviewComposer
{
    public static PrizePreviewDto Compose(PrizeEvaluationInputs inputs, IPrizeEvaluator evaluator, IDateTimeProvider dateTimeProvider)
    {
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
