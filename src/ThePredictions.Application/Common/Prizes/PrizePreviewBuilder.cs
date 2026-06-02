using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Diffs the breakdown at N against N+1 to produce the prospective-member "+£x" view: the
/// projected (N+1) breakdown annotated with per-slot and per-category deltas, plus attribution copy.
/// </summary>
public static class PrizePreviewBuilder
{
    public static (PrizeBreakdownDto Breakdown, List<string> Attribution) Build(PrizeBreakdownDto current, PrizeBreakdownDto projected, decimal entryCost)
    {
        var currentCategories = current.Categories.ToDictionary(c => c.Category);

        var annotatedCategories = projected.Categories.Select(projectedCategory =>
        {
            currentCategories.TryGetValue(projectedCategory.Category, out var currentCategory);
            var currentSlots = (currentCategory?.Slots ?? []).ToDictionary(SlotKey);

            var annotatedSlots = projectedCategory.Slots.Select(slot =>
            {
                var previousAmount = currentSlots.TryGetValue(SlotKey(slot), out var previous) ? previous.Amount : 0m;
                return new PrizeSlotDto
                {
                    Label = slot.Label,
                    Amount = slot.Amount,
                    Rank = slot.Rank,
                    StageName = slot.StageName,
                    Delta = slot.Amount - previousAmount
                };
            }).ToList();

            return new PrizeCategoryBreakdownDto
            {
                Category = projectedCategory.Category,
                DisplayName = projectedCategory.DisplayName,
                Kind = projectedCategory.Kind,
                SubPot = projectedCategory.SubPot,
                Delta = projectedCategory.SubPot - (currentCategory?.SubPot ?? 0m),
                Slots = annotatedSlots
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
            .Select(c => $"£{c.Delta:0} to the {c.DisplayName.ToLowerInvariant()} prize{(c.Slots.Count > 1 ? "s" : string.Empty)}")
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

    private static string SlotKey(PrizeSlotDto slot) => $"{slot.Rank}|{slot.StageName}|{slot.Label}";
}
