using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// The extensible catalogue of prize categories. Adding a future prize type is one row here (plus a
/// strategy in the settlement engine if it scores differently). It also owns the product-default
/// places table and the recommended per-entry split (renormalised default weights across whatever
/// categories are enabled).
/// </summary>
public static class PrizeCategoryRegistry
{
    private static readonly IReadOnlyList<PrizeCategoryDefinition> Definitions = new[]
    {
        new PrizeCategoryDefinition(PrizeType.Overall, PrizeCategoryKind.EndOfSeason, 3, CategoryAvailability.All, IsRanked: true, "Overall"),
        new PrizeCategoryDefinition(PrizeType.Stages, PrizeCategoryKind.Staged, 2, CategoryAvailability.TournamentsOnly, IsRanked: true, "Stages"),
        new PrizeCategoryDefinition(PrizeType.MostExactScores, PrizeCategoryKind.EndOfSeason, 1, CategoryAvailability.All, IsRanked: false, "Most Exact Scores"),
        new PrizeCategoryDefinition(PrizeType.Round, PrizeCategoryKind.Recurring, 1, CategoryAvailability.All, IsRanked: false, "Round"),
        new PrizeCategoryDefinition(PrizeType.Monthly, PrizeCategoryKind.Recurring, 1, CategoryAvailability.SeasonsOnly, IsRanked: false, "Monthly")
    };

    /// <summary>The product-default places table (ADR-0011). Used unless a league overrides it.</summary>
    public static readonly RankTable DefaultRankTable = new(new[]
    {
        new RankBand(2, 5, new[] { 100 }),
        new RankBand(6, 10, new[] { 70, 30 }),
        new RankBand(11, 20, new[] { 50, 30, 20 }),
        new RankBand(21, 40, new[] { 50, 25, 15, 10 }),
        new RankBand(41, 75, new[] { 40, 25, 15, 12, 8 }),
        new RankBand(76, null, new[] { 35, 22, 15, 12, 9, 7 })
    });

    public static PrizeCategoryDefinition Definition(PrizeType category) =>
        Definitions.FirstOrDefault(d => d.Category == category)
        ?? throw new ArgumentOutOfRangeException(nameof(category), category, "No prize category definition is registered for this type.");

    /// <summary>
    /// Whether a prize is settled at the end of the season rather than as the season runs.
    /// </summary>
    /// <remarks>
    /// The winnings page groups prizes into four buckets - rounds, months, tournament stages, and everything else - and
    /// stated that last one as <c>PrizeType != Round &amp;&amp; PrizeType != Monthly &amp;&amp; PrizeType != Stages</c>
    /// three times over. Written as a negation it also silently absorbs any prize type added later, which is the right
    /// default for a bucket called "everything else" but only if that is deliberate.
    /// </remarks>
    public static bool IsEndOfSeason(PrizeType category) =>
        category is not (PrizeType.Round or PrizeType.Monthly or PrizeType.Stages);

    public static bool IsAvailable(PrizeType category, bool isTournament)
    {
        var availability = Definition(category).AvailableFor;

        // CategoryAvailability.All is the default arm: a switch expression over an enum always
        // needs one, and making it the "available everywhere" case keeps it reachable.
        return availability switch
        {
            CategoryAvailability.SeasonsOnly => !isTournament,
            CategoryAvailability.TournamentsOnly => isTournament,
            _ => true
        };
    }

    public static IReadOnlyList<PrizeCategoryDefinition> AvailableCategories(bool isTournament) =>
        Definitions.Where(d => IsAvailable(d.Category, isTournament)).ToList();

    /// <summary>
    /// Renormalises the default weights across the enabled categories and converts them into
    /// whole-pound per-entry amounts that sum to the stake (leftover handed out top-down by weight).
    /// </summary>
    public static IReadOnlyDictionary<PrizeType, int> RecommendedAllocation(IEnumerable<PrizeType> enabledCategories, int stakePounds)
    {
        var enabled = enabledCategories.Distinct().ToList();
        if (enabled.Count == 0)
            return new Dictionary<PrizeType, int>();

        var weights = enabled.Select(c => Definition(c).DefaultWeight).ToList();
        var amounts = AllocateByWeight(stakePounds, weights);

        var result = new Dictionary<PrizeType, int>();
        for (var i = 0; i < enabled.Count; i++)
            result[enabled[i]] = amounts[i];

        return result;
    }

    private static int[] AllocateByWeight(int total, IReadOnlyList<int> weights)
    {
        var count = weights.Count;
        var result = new int[count];
        if (total <= 0)
            return result;

        var weightSum = weights.Sum();
        var allocated = 0;
        for (var i = 0; i < count; i++)
        {
            result[i] = (int)((long)total * weights[i] / weightSum);
            allocated += result[i];
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
}
