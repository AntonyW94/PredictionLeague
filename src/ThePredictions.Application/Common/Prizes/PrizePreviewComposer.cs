using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Builds the prospective-member <see cref="PrizePreviewDto"/> from loaded evaluation inputs:
/// headline facts, the projected breakdown if they join, and the attributed "+£x" effect of their
/// own entry. Shared by the by-id and by-code preview query handlers.
/// </summary>
public static class PrizePreviewComposer
{
    public static PrizePreviewDto Compose(PrizeEvaluationInputs inputs, IPrizeEvaluator evaluator, IDateTimeProvider dateTimeProvider, LeaguePaymentInfoDto? payment = null)
    {
        // Through the same rule the rest of the site uses, which is also where "a league with no deadline is not open" is
        // stated - so a league saved without one shows a final pot rather than inviting somebody to join it.
        var deadlinePassed = !LeagueEntry.IsOpen(inputs.EntryDeadlineUtc, dateTimeProvider.UtcNow);
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
                // After the deadline the pot is final - show the current breakdown, no joining split.
                breakdown = current;
            }
            else
            {
                var projected = evaluator.Evaluate(inputs.ToEvaluationRequest(inputs.EntrantCount + 1));
                var perEntryByCategory = inputs.Categories.ToDictionary(c => c.Category, c => c.PerEntryPounds);
                (breakdown, attribution) = PrizePreviewBuilder.Build(current, projected, perEntryByCategory, inputs.EntryCost);
                projectedPot = projected.Pot;
            }
        }

        return new PrizePreviewDto
        {
            LeagueId = inputs.LeagueId,
            LeagueName = inputs.LeagueName,
            SeasonName = inputs.SeasonName,
            AdministratorName = inputs.AdministratorName,
            EntrantCount = inputs.EntrantCount,
            EntryCost = inputs.EntryCost,
            CurrentPrizePot = currentPot,
            ProjectedPrizePot = projectedPot,
            EntryDeadlineUtc = inputs.EntryDeadlineUtc,
            DeadlinePassed = deadlinePassed,
            HasPrizes = hasPrizes,
            Breakdown = breakdown,
            Attribution = attribution,
            Payment = payment
        };
    }
}
