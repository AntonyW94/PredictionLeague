using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Annotates the projected (N+1) breakdown for the prospective-member "+£x" view. The per-category
/// "+£x" is the joiner's own per-entry allocation to that prize fund (what the admin configured the
/// entry fee to be split into) - so every funded category reflects the entry, and the figures are
/// stable rather than lumpy. (The headline prizes themselves still move in clean blocks as the pot
/// rounds and spillover flows, which is a separate display concern.)
/// </summary>
public static class PrizePreviewBuilder
{
    public static (PrizeBreakdownDto Breakdown, List<string> Attribution) Build(
        PrizeBreakdownDto projected,
        IReadOnlyDictionary<PrizeType, int> perEntryByCategory,
        decimal entryCost)
    {
        var annotatedCategories = projected.Categories.Select(category =>
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
                // do not map cleanly onto a single joiner's entry. The category contribution is the
                // honest, stable figure.
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
            Pot = projected.Pot,
            EntrantCount = projected.EntrantCount,
            Categories = annotatedCategories
        };

        var attribution = BuildAttribution(annotatedCategories, entryCost);

        return (annotated, attribution);
    }

    private static List<string> BuildAttribution(IReadOnlyList<PrizeCategoryBreakdownDto> categories, decimal entryCost)
    {
        var contributions = categories
            .Where(c => c.Delta is > 0)
            .Select(c => $"£{c.Delta:0} to {c.DisplayName.ToLowerInvariant()}")
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
