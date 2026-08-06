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

        var state = new ApportionmentState
        {
            Categories = request.Categories,
            SubPot = BuildSubPots(request)
        };

        // Order matters: spillover flows forward only (Section -> Overall -> Most Exact Scores),
        // so each step must run after every step that can spill into it.
        ApportionRecurring(request, state);
        ApportionSection(request, state);
        ApportionOverall(request, state);
        ApportionMostExactScores(state);

        return new PrizeBreakdown
        {
            PotPounds = request.StakePounds * request.EntrantCount + request.AdminTopUpPounds,
            Categories = BuildCategories(state)
        };
    }

    /// <summary>
    /// Each category's sub-pot: per-entry money plus a weighted share of the admin top-up.
    /// </summary>
    private static Dictionary<PrizeType, int> BuildSubPots(PrizeApportionmentRequest request)
    {
        var categories = request.Categories;
        var topUpShares = Distribute(request.AdminTopUpPounds, categories.Select(c => c.PerEntryPounds).ToList());

        var subPot = new Dictionary<PrizeType, int>();
        for (var i = 0; i < categories.Count; i++)
            subPot[categories[i].Category] = categories[i].PerEntryPounds * request.EntrantCount + topUpShares[i];

        return subPot;
    }

    /// <summary>
    /// Step 1 - Recurring (Round, Monthly): uniform per-event prize; the leftover spills OUT of the
    /// category to the first available absorbing category, so per-event prizes never go uneven.
    /// </summary>
    private static void ApportionRecurring(PrizeApportionmentRequest request, ApportionmentState state)
    {
        var recurringRemainder = 0;
        foreach (var category in state.Categories.Where(c => c.Kind == PrizeCategoryKind.Recurring))
        {
            var events = category.Category == PrizeType.Monthly ? request.NumberOfMonths : request.NumberOfRounds;
            var sub = state.SubPot[category.Category];
            var perEvent = events > 0 ? sub / events : 0;
            recurringRemainder += sub - perEvent * events;

            var categorySlots = new List<PrizeBreakdownSlot>();
            if (perEvent > 0)
                categorySlots.Add(new PrizeBreakdownSlot { Label = EventLabel(category.Category), Amount = perEvent });

            state.Slots[category.Category] = categorySlots;
            state.CategoryTotal[category.Category] = perEvent * events;
        }

        if (recurringRemainder > 0)
            RouteRecurringSpillover(state, recurringRemainder);
    }

    /// <summary>
    /// Spillover destination priority: Most Exact Scores -> Overall -> Section. When no category can
    /// absorb it (a recurring-only scheme), it becomes a one-off bonus on the final event.
    /// </summary>
    private static void RouteRecurringSpillover(ApportionmentState state, int remainder)
    {
        if (state.Enabled(PrizeType.MostExactScores))
            state.SpillToExact += remainder;
        else if (state.Enabled(PrizeType.Overall))
            state.SpillToOverall += remainder;
        else if (state.Enabled(PrizeType.Stages))
            state.SpillToSection += remainder;
        else
            AddFinalEventBonus(state, remainder);
    }

    /// <summary>
    /// Step 2 - Section (Staged): two stages 50/50, each ranked and £5-rounded like Overall.
    /// </summary>
    private static void ApportionSection(PrizeApportionmentRequest request, ApportionmentState state)
    {
        if (!state.Enabled(PrizeType.Stages))
            return;

        var sectionAllocation = state.Categories.First(c => c.Category == PrizeType.Stages);
        var sectionSub = state.SubPot[PrizeType.Stages] + state.SpillToSection;
        var percentages = sectionAllocation.RankTable?.PercentagesFor(request.EntrantCount) ?? SinglePlace;
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
            if (state.Enabled(PrizeType.MostExactScores))
                state.SpillToExact += sectionRemainder;
            else if (state.Enabled(PrizeType.Overall))
                state.SpillToOverall += sectionRemainder;
            else
                AddRemainderToTopRank(sectionSlots, sectionRemainder);
        }

        state.Slots[PrizeType.Stages] = sectionSlots;
        state.CategoryTotal[PrizeType.Stages] = sectionSlots.Sum(s => (int)s.Amount);
    }

    /// <summary>
    /// Step 3 - Overall: ranked places, £5-rounded once a place pays more than £5.
    /// </summary>
    private static void ApportionOverall(PrizeApportionmentRequest request, ApportionmentState state)
    {
        if (!state.Enabled(PrizeType.Overall))
            return;

        var overallAllocation = state.Categories.First(c => c.Category == PrizeType.Overall);
        var overallSub = state.SubPot[PrizeType.Overall] + state.SpillToOverall;
        var percentages = overallAllocation.RankTable?.PercentagesFor(request.EntrantCount) ?? SinglePlace;

        var overallSlots = new List<PrizeBreakdownSlot>();
        var (amounts, remainder) = ApportionRanked(overallSub, percentages);
        AddRankSlots(overallSlots, amounts);

        if (remainder > 0)
        {
            if (state.Enabled(PrizeType.MostExactScores))
                state.SpillToExact += remainder;
            else
                AddRemainderToTopRank(overallSlots, remainder);
        }

        state.Slots[PrizeType.Overall] = overallSlots;
        state.CategoryTotal[PrizeType.Overall] = overallSlots.Sum(s => (int)s.Amount);
    }

    /// <summary>
    /// Step 4 - Most Exact Scores: a single £1-granular prize and the final spillover sink.
    /// </summary>
    private static void ApportionMostExactScores(ApportionmentState state)
    {
        if (!state.Enabled(PrizeType.MostExactScores))
            return;

        var exactSub = state.SubPot[PrizeType.MostExactScores] + state.SpillToExact;
        var exactSlots = new List<PrizeBreakdownSlot>();
        if (exactSub > 0)
            exactSlots.Add(new PrizeBreakdownSlot { Label = "Most Exact Scores", Amount = exactSub, Rank = 1 });

        state.Slots[PrizeType.MostExactScores] = exactSlots;
        state.CategoryTotal[PrizeType.MostExactScores] = exactSub;
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

    private static void AddFinalEventBonus(ApportionmentState state, int remainder)
    {
        // Only reached when the scheme has no absorbing category (recurring-only). Keep per-event
        // prizes uniform by adding a distinct one-off bonus on the final event rather than skewing them.
        var firstRecurring = state.Categories.First(c => c.Kind == PrizeCategoryKind.Recurring);
        var label = firstRecurring.Category == PrizeType.Monthly ? "Final month bonus" : "Final round bonus";
        state.Slots[firstRecurring.Category].Add(new PrizeBreakdownSlot { Label = label, Amount = remainder });
        state.CategoryTotal[firstRecurring.Category] += remainder;
    }

    private static IReadOnlyList<PrizeCategoryBreakdown> BuildCategories(ApportionmentState state)
    {
        // Stable display order regardless of the order categories were configured in.
        var displayOrder = new[] { PrizeType.Overall, PrizeType.Stages, PrizeType.Round, PrizeType.Monthly, PrizeType.MostExactScores };

        return displayOrder
            .Where(state.Slots.ContainsKey)
            .Select(type => new PrizeCategoryBreakdown
            {
                Category = type,
                Kind = state.Categories.First(c => c.Category == type).Kind,
                SubPotPounds = state.CategoryTotal[type],
                Slots = state.Slots[type]
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

    /// <summary>
    /// The working state threaded through the four apportionment steps: the fixed inputs, the
    /// breakdown built up so far, and the three forward-flowing spillover accumulators. Kept
    /// private to the engine - <see cref="Apportion"/> stays a pure function of its request.
    /// </summary>
    private sealed class ApportionmentState
    {
        public required IReadOnlyList<PrizeCategoryAllocation> Categories { get; init; }
        public required IReadOnlyDictionary<PrizeType, int> SubPot { get; init; }

        public Dictionary<PrizeType, List<PrizeBreakdownSlot>> Slots { get; } = new();
        public Dictionary<PrizeType, int> CategoryTotal { get; } = new();

        public int SpillToExact { get; set; }
        public int SpillToOverall { get; set; }
        public int SpillToSection { get; set; }

        /// <summary>A category is enabled precisely when the scheme allocated it a sub-pot.</summary>
        public bool Enabled(PrizeType type) => SubPot.ContainsKey(type);
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
