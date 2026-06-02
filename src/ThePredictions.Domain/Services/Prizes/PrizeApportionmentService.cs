using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// Pure, deterministic apportionment of a prize scheme into a concrete round-number breakdown.
/// No randomness, no stored state - the breakdown is a function of (scheme, pot, entrant count),
/// so it recomputes (and spillover can reverse) as entrants change, and always sums to the pot.
///
/// Rules (see ADR-0011):
/// - Pot = StakePounds * N + AdminTopUpPounds. Each category's sub-pot = perEntry * N plus its
///   weighted share of the admin top-up.
/// - Recurring (Round/Monthly): uniform whole-pound prize per event = floor(subPot / events);
///   the leftover spills OUT of the category (never making per-event prizes uneven).
/// - Overall: places from the rank table; above the £5 threshold every rank rounds to a clean £5
///   and the odd £1-£4 spills out; below it, £1-granular top-down.
/// - Section (Staged): split 50/50 across the two stages, each ranked by the table at £1.
/// - Most Exact Scores: a single £1-granular prize and the final spillover "sink".
/// - Spillover destination priority: Most Exact Scores -> Overall -> Section (forward-flowing,
///   so no cycles). Overall's own £5 remainder goes to Most Exact Scores, else onto 1st place.
///
/// Note: a Recurring category's slot shows the per-event prize, so its
/// <see cref="PrizeCategoryBreakdown.SubPotPounds"/> (its share of the pot) is tracked separately
/// from the slot amount. Summing <c>SubPotPounds</c> across categories always equals the pot.
/// </summary>
public static class PrizeApportionmentService
{
    private const string GroupStageName = "Group stage";
    private const string KnockoutStageName = "Knockout stage";
    private static readonly IReadOnlyList<int> SinglePlace = new[] { 100 };

    public static PrizeBreakdown Apportion(PrizeApportionmentRequest request)
    {
        Guard.Against.Null(request);
        Guard.Against.Negative(request.EntrantCount);
        Guard.Against.Negative(request.StakePounds);
        Guard.Against.Negative(request.AdminTopUpPounds);
        Guard.Against.Negative(request.OverallFivePoundThreshold);

        var categories = request.Categories;
        var n = request.EntrantCount;
        var pot = request.StakePounds * n + request.AdminTopUpPounds;

        // Each category's sub-pot: per-entry money plus a weighted share of the admin top-up.
        var topUpShares = Distribute(request.AdminTopUpPounds, categories.Select(c => c.PerEntryPounds).ToList());
        var subPot = new Dictionary<PrizeType, int>();
        for (var i = 0; i < categories.Count; i++)
            subPot[categories[i].Category] = categories[i].PerEntryPounds * n + topUpShares[i];

        bool Enabled(PrizeType type) => subPot.ContainsKey(type);

        var slots = new Dictionary<PrizeType, List<PrizeBreakdownSlot>>();
        var categoryTotal = new Dictionary<PrizeType, int>();
        var spillToExact = 0;
        var spillToOverall = 0;
        var spillToSection = 0;

        // Step 1 - Recurring (Round, Monthly): uniform per-event prize; collect the leftover to spill.
        var recurringRemainder = 0;
        foreach (var category in categories.Where(c => c.Kind == PrizeCategoryKind.Recurring))
        {
            var events = category.Category == PrizeType.Monthly ? request.NumberOfMonths : request.NumberOfRounds;
            var sub = subPot[category.Category];
            var perEvent = events > 0 ? sub / events : 0;
            recurringRemainder += sub - perEvent * events;

            var categorySlots = new List<PrizeBreakdownSlot>();
            if (perEvent > 0)
                categorySlots.Add(new PrizeBreakdownSlot { Label = EventLabel(category.Category), Amount = perEvent });

            slots[category.Category] = categorySlots;
            categoryTotal[category.Category] = perEvent * events;
        }

        // Route the recurring leftover to the first available absorbing category (forward-flowing).
        if (recurringRemainder > 0)
        {
            if (Enabled(PrizeType.MostExactScores))
                spillToExact += recurringRemainder;
            else if (Enabled(PrizeType.Overall))
                spillToOverall += recurringRemainder;
            else if (Enabled(PrizeType.Section))
                spillToSection += recurringRemainder;
            else
                AddFinalEventBonus(categories, slots, categoryTotal, recurringRemainder);
        }

        // Step 2 - Section (Staged): two stages 50/50, each ranked by the table at £1 granularity.
        if (Enabled(PrizeType.Section))
        {
            var sectionAllocation = categories.First(c => c.Category == PrizeType.Section);
            var sectionSub = subPot[PrizeType.Section] + spillToSection;
            var percentages = sectionAllocation.RankTable?.PercentagesFor(n) ?? SinglePlace;
            var stagePots = Distribute(sectionSub, new[] { 1, 1 });

            var sectionSlots = new List<PrizeBreakdownSlot>();
            AddStageSlots(sectionSlots, GroupStageName, stagePots[0], percentages);
            AddStageSlots(sectionSlots, KnockoutStageName, stagePots[1], percentages);

            slots[PrizeType.Section] = sectionSlots;
            categoryTotal[PrizeType.Section] = sectionSub;
        }

        // Step 3 - Overall: ranked places, with £5 rounding above the threshold.
        if (Enabled(PrizeType.Overall))
        {
            var overallAllocation = categories.First(c => c.Category == PrizeType.Overall);
            var overallSub = subPot[PrizeType.Overall] + spillToOverall;
            var percentages = overallAllocation.RankTable?.PercentagesFor(n) ?? SinglePlace;
            var overallSlots = new List<PrizeBreakdownSlot>();

            if (overallSub >= request.OverallFivePoundThreshold && overallSub >= 5)
            {
                var floored = overallSub - overallSub % 5;
                var remainder = overallSub - floored;
                var unitsPerRank = Distribute(floored / 5, percentages);
                AddRankSlots(overallSlots, unitsPerRank, units => units * 5);

                if (remainder > 0)
                {
                    if (Enabled(PrizeType.MostExactScores))
                        spillToExact += remainder;
                    else
                        AddRemainderToTopRank(overallSlots, remainder);
                }
            }
            else
            {
                var amountsPerRank = Distribute(overallSub, percentages);
                AddRankSlots(overallSlots, amountsPerRank, amount => amount);
            }

            slots[PrizeType.Overall] = overallSlots;
            categoryTotal[PrizeType.Overall] = overallSlots.Sum(s => (int)s.Amount);
        }

        // Step 4 - Most Exact Scores: a single prize and the final spillover sink.
        if (Enabled(PrizeType.MostExactScores))
        {
            var exactSub = subPot[PrizeType.MostExactScores] + spillToExact;
            var exactSlots = new List<PrizeBreakdownSlot>();
            if (exactSub > 0)
                exactSlots.Add(new PrizeBreakdownSlot { Label = "Most exact scores", Amount = exactSub, Rank = 1 });

            slots[PrizeType.MostExactScores] = exactSlots;
            categoryTotal[PrizeType.MostExactScores] = exactSub;
        }

        return new PrizeBreakdown
        {
            PotPounds = pot,
            Categories = BuildCategories(categories, slots, categoryTotal)
        };
    }

    private static string EventLabel(PrizeType category) => category == PrizeType.Monthly ? "Per month" : "Per round";

    private static void AddStageSlots(List<PrizeBreakdownSlot> target, string stageName, int stagePot, IReadOnlyList<int> percentages)
    {
        var amounts = Distribute(stagePot, percentages);
        for (var rank = 0; rank < amounts.Length; rank++)
        {
            if (amounts[rank] > 0)
                target.Add(new PrizeBreakdownSlot { Label = $"{stageName} - {Ordinal(rank + 1)}", Amount = amounts[rank], Rank = rank + 1, StageName = stageName });
        }
    }

    private static void AddRankSlots(List<PrizeBreakdownSlot> target, int[] perRank, Func<int, int> toPounds)
    {
        for (var rank = 0; rank < perRank.Length; rank++)
        {
            var amount = toPounds(perRank[rank]);
            if (amount > 0)
                target.Add(new PrizeBreakdownSlot { Label = Ordinal(rank + 1), Amount = amount, Rank = rank + 1 });
        }
    }

    private static void AddRemainderToTopRank(List<PrizeBreakdownSlot> overallSlots, int remainder)
    {
        // Reached only above the £5 threshold, where the floored sub-pot is >= £5, so 1st place
        // always exists (the top-down leftover lands on it first).
        var top = overallSlots[0];
        overallSlots[0] = new PrizeBreakdownSlot { Label = top.Label, Amount = top.Amount + remainder, Rank = top.Rank, StageName = top.StageName };
    }

    private static void AddFinalEventBonus(IReadOnlyList<PrizeCategoryAllocation> categories, Dictionary<PrizeType, List<PrizeBreakdownSlot>> slots, Dictionary<PrizeType, int> categoryTotal, int remainder)
    {
        // Only reached when the scheme has no absorbing category (recurring-only). Keep per-event
        // prizes uniform by adding a distinct one-off bonus on the final event rather than skewing them.
        var firstRecurring = categories.First(c => c.Kind == PrizeCategoryKind.Recurring);
        var label = firstRecurring.Category == PrizeType.Monthly ? "Final month bonus" : "Final round bonus";
        slots[firstRecurring.Category].Add(new PrizeBreakdownSlot { Label = label, Amount = remainder });
        categoryTotal[firstRecurring.Category] += remainder;
    }

    private static IReadOnlyList<PrizeCategoryBreakdown> BuildCategories(IReadOnlyList<PrizeCategoryAllocation> categories, Dictionary<PrizeType, List<PrizeBreakdownSlot>> slots, Dictionary<PrizeType, int> categoryTotal)
    {
        // Stable display order regardless of the order categories were configured in.
        var displayOrder = new[] { PrizeType.Overall, PrizeType.Section, PrizeType.Round, PrizeType.Monthly, PrizeType.MostExactScores };

        return displayOrder
            .Where(slots.ContainsKey)
            .Select(type => new PrizeCategoryBreakdown
            {
                Category = type,
                Kind = categories.First(c => c.Category == type).Kind,
                SubPotPounds = categoryTotal[type],
                Slots = slots[type]
            })
            .ToList();
    }

    /// <summary>
    /// Splits <paramref name="total"/> whole pounds across slots weighted by <paramref name="weights"/>,
    /// handing any leftover out top-down (slot 0 first) so growth is monotonic and the top slot never
    /// rounds down. Falls back to an equal split when the weights sum to zero.
    /// </summary>
    private static int[] Distribute(int total, IReadOnlyList<int> weights)
    {
        var count = weights.Count;
        var result = new int[count];
        if (total <= 0)
            return result;

        var weightSum = weights.Sum();
        var allocated = 0;

        if (weightSum <= 0)
        {
            var basePer = total / count;
            for (var i = 0; i < count; i++)
                result[i] = basePer;
            allocated = basePer * count;
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                result[i] = (int)((long)total * weights[i] / weightSum);
                allocated += result[i];
            }
        }

        var leftover = total - allocated;
        var index = 0;
        while (leftover > 0)
        {
            result[index] += 1;
            leftover -= 1;
            index = (index + 1) % count;
        }

        return result;
    }

    private static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13)
            return $"{number}th";

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }
}
