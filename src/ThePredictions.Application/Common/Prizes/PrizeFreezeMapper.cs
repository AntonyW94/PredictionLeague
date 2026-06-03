using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Converts a live prize breakdown into the frozen <see cref="LeaguePrizeSetting"/> rows the existing
/// settlement engine consumes. Recurring categories collapse to a single per-event setting; ranked
/// and staged categories produce one setting per paid slot.
/// </summary>
public static class PrizeFreezeMapper
{
    public static List<LeaguePrizeSetting> ToPrizeSettings(PrizeBreakdownDto breakdown, int leagueId)
    {
        var settings = new List<LeaguePrizeSetting>();

        foreach (var category in breakdown.Categories)
        {
            if (category.Kind == PrizeCategoryKind.Recurring)
            {
                // One uniform per-event prize (the first, per-event slot). Settled each event.
                var perEvent = category.Slots.FirstOrDefault(s => s.Rank is null) ?? category.Slots.FirstOrDefault();
                if (perEvent is { Amount: > 0 })
                    settings.Add(LeaguePrizeSetting.Create(leagueId, category.Category, 1, perEvent.Amount, prizeDescription: perEvent.Label));

                continue;
            }

            foreach (var slot in category.Slots.Where(s => s.Amount > 0 && s.Rank.HasValue))
                settings.Add(LeaguePrizeSetting.Create(leagueId, category.Category, slot.Rank!.Value, slot.Amount, slot.StageName, slot.Label));
        }

        return settings;
    }
}
