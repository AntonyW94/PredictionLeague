using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Builds the prospective-member "+£x" view. The displayed prizes are the CURRENT breakdown (the
/// prizes as they stand now), and the per-category "+£x" is the joiner's per-entry allocation to
/// that prize fund - the split the admin configured in the league settings. That split is the same
/// for everyone and never lumpy, unlike the actual rounded prize movement of a single join. The pot
/// figure is the projected (N+1) total, so current prizes + the split reconcile to "pot if you join".
/// </summary>
public static class PrizePreviewBuilder
{
    public static (PrizeBreakdownDto Breakdown, List<string> Attribution) Build(
        PrizeBreakdownDto current,
        PrizeBreakdownDto projected,
        IReadOnlyDictionary<PrizeType, int> perEntryByCategory,
        decimal entryCost)
    {
        var annotatedCategories = current.Categories.Select(category =>
        {
            var contribution = perEntryByCategory.TryGetValue(category.Category, out var perEntry) ? perEntry : 0;

            return new PrizeCategoryBreakdownDto
            {
                Category = category.Category,
                DisplayName = category.DisplayName,
                Kind = category.Kind,
                SubPot = category.SubPot,
                Delta = contribution,
                // Per-slot deltas are intentionally omitted: with block rounding they are lumpy and
                // do not map cleanly onto a single entry. The category split is the stable figure.
                Slots = category.Slots.Select(slot => new PrizeSlotDto
                {
                    Label = slot.Label,
                    Amount = slot.Amount,
                    Rank = slot.Rank,
                    StageName = slot.StageName,
                    Delta = null
                }).ToList()
            };
        }).ToList();

        var annotated = new PrizeBreakdownDto
        {
            // Headline shows the pot the joiner would create; the category amounts above are current.
            Pot = projected.Pot,
            EntrantCount = current.EntrantCount,
            Categories = annotatedCategories
        };

        var attribution = BuildAttribution(annotatedCategories, entryCost);

        return (annotated, attribution);
    }

    private static List<string> BuildAttribution(IReadOnlyList<PrizeCategoryBreakdownDto> categories, decimal entryCost)
    {
        // GetValueOrDefault, not a null-aware pattern: every category built above carries a Delta,
        // so a null test here would be a branch that can never go the other way.
        var contributions = categories
            .Where(c => c.Delta.GetValueOrDefault() > 0)
            .Select(c => $"£{c.Delta:0} to {c.DisplayName}")
            .ToList();

        if (contributions.Count == 0)
            return [];

        return [$"Your £{entryCost:0} adds {JoinHumanly(contributions)}."];
    }

    private static string JoinHumanly(IReadOnlyList<string> parts)
    {
        if (parts.Count == 1)
            return parts[0];

        return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
    }
}
