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
/// - Placed prizes (Overall, Section): once a place would pay more than £5 the whole category is
///   distributed in clean £5 chunks (top-down); the odd £1-£4 spills out. Tiny funds (top place
///   <= £5) stay £1-granular so small/early pots are not distorted.
/// - Recurring (Round/Monthly): uniform whole-pound prize per event = floor(subPot / events); the
///   leftover spills OUT of the category (never making per-event prizes uneven).
/// - Most Exact Scores: a single £1-granular prize and the final spillover "sink".
/// - Spillover destination priority: Most Exact Scores -> Overall -> Section (forward-flowing, so
///   no cycles); when none can absorb it, the odd pounds fall on 1st place.
///
/// Note: a Recurring category's slot shows the per-event prize, so its
/// <see cref="PrizeCategoryBreakdown.SubPotPounds"/> is tracked separately from the slot amount.
/// Summing <c>SubPotPounds</c> across categories always equals the pot.
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

        // Step 2 - Section (Staged): two stages 50/50, each ranked and £5-rounded like Overall.
        if (Enabled(PrizeType.Section))
        {
            var sectionAllocation = categories.First(c => c.Category == PrizeType.Section);
            var sectionSub = subPot[PrizeType.Section] + spillToSection;
            var percentages = sectionAllocation.RankTable?.PercentagesFor(n) ?? SinglePlace;
            var stagePots = Distribute(sectionSub, new[] { 1, 1 });
            var stageNames = new[] { GroupStageName, KnockoutStageName };

            var sectionSlots = new List<PrizeBreakdownSlot>();
            var sectionRemainder = 0;
            for (var stage = 0; stage < stageNames.Length; stage++)
            {
                var (amounts, remainder) = ApportionRanked(stagePots[stage], percentages);
                AddRankSlots(sectionSlots, amounts, stageNames[stage]);
                sectionRemainder += remainder;
            }

            if (sectionRemainder > 0)
            {
                if (Enabled(PrizeType.MostExactScores))
                    spillToExact += sectionRemainder;
                else if (Enabled(PrizeType.Overall))
                    spillToOverall += sectionRemainder;
                else
                    AddRemainderToTopRank(sectionSlots, sectionRemainder);
            }

            slots[PrizeType.Section] = sectionSlots;
            categoryTotal[PrizeType.Section] = sectionSlots.Sum(s => (int)s.Amount);
        }

        // Step 3 - Overall: ranked places, £5-rounded once a place pays more than £5.
        if (Enabled(PrizeType.Overall))
        {
            var overallAllocation = categories.First(c => c.Category == PrizeType.Overall);
            var overallSub = subPot[PrizeType.Overall] + spillToOverall;
            var percentages = overallAllocation.RankTable?.PercentagesFor(n) ?? SinglePlace;

            var overallSlots = new List<PrizeBreakdownSlot>();
            var (amounts, remainder) = ApportionRanked(overallSub, percentages);
            AddRankSlots(overallSlots, amounts);

            if (remainder > 0)
            {
                if (Enabled(PrizeType.MostExactScores))
                    spillToExact += remainder;
                else
                    AddRemainderToTopRank(overallSlots, remainder);
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
                exactSlots.Add(new PrizeBreakdownSlot { Label = "Most Exact Scores", Amount = exactSub, Rank = 1 });

            slots[PrizeType.MostExactScores] = exactSlots;
            categoryTotal[PrizeType.MostExactScores] = exactSub;
        }

        return new PrizeBreakdown
        {
            PotPounds = pot,
            Categories = BuildCategories(categories, slots, categoryTotal)
        };
    }

    /// <summary>
    /// Apportions a placed-prize fund across the ranks. Once the top place would pay more than £5
    /// the whole fund is handed out in clean £5 chunks (top-down) and the odd £1-£4 is returned to
    /// spill elsewhere; otherwise the fund stays £1-granular (small pots aren't distorted).
    /// </summary>
    private static (int[] Amounts, int Remainder) ApportionRanked(int fund, IReadOnlyList<int> percentages)
    {
        var natural = Distribute(fund, percentages);
        if (natural[0] <= 5)
            return (natural, 0);

        var floored = fund - fund % 5;
        var unitsPerRank = Distribute(floored / 5, percentages);
        var amounts = new int[unitsPerRank.Length];
        for (var i = 0; i < unitsPerRank.Length; i++)
            amounts[i] = unitsPerRank[i] * 5;

        return (amounts, fund - floored);
    }

    private static string EventLabel(PrizeType category) => category == PrizeType.Monthly ? "Per month" : "Per round";

    private static void AddRankSlots(List<PrizeBreakdownSlot> target, int[] amounts, string? stageName = null)
    {
        for (var rank = 0; rank < amounts.Length; rank++)
        {
            if (amounts[rank] <= 0)
                continue;

            var label = stageName is null ? Ordinal(rank + 1) : $"{stageName} - {Ordinal(rank + 1)}";
            target.Add(new PrizeBreakdownSlot { Label = label, Amount = amounts[rank], Rank = rank + 1, StageName = stageName });
        }
    }

    private static void AddRemainderToTopRank(List<PrizeBreakdownSlot> rankedSlots, int remainder)
    {
        // Reached only when a place already pays >= £5, so 1st place exists (top-down leftover lands on it).
        var top = rankedSlots[0];
        rankedSlots[0] = new PrizeBreakdownSlot { Label = top.Label, Amount = top.Amount + remainder, Rank = top.Rank, StageName = top.StageName };
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
