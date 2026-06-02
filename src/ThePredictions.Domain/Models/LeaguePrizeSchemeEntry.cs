using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

/// <summary>
/// One enabled prize category within a <see cref="LeaguePrizeScheme"/>: the whole-pound share of
/// each entry that funds it, and an optional per-league override of the places table (stored as
/// JSON, parsed by the application evaluator).
/// </summary>
public class LeaguePrizeSchemeEntry
{
    public int Id { get; init; }
    public int LeaguePrizeSchemeId { get; private set; }
    public PrizeType Category { get; private set; }
    public int PerEntryPounds { get; private set; }
    public string? RankTableJson { get; private set; }

    private LeaguePrizeSchemeEntry() { }

    public LeaguePrizeSchemeEntry(int id, int leaguePrizeSchemeId, PrizeType category, int perEntryPounds, string? rankTableJson)
    {
        Id = id;
        LeaguePrizeSchemeId = leaguePrizeSchemeId;
        Category = category;
        PerEntryPounds = perEntryPounds;
        RankTableJson = rankTableJson;
    }

    public static LeaguePrizeSchemeEntry Create(PrizeType category, int perEntryPounds, string? rankTableJson = null)
    {
        Guard.Against.Negative(perEntryPounds);

        return new LeaguePrizeSchemeEntry
        {
            Category = category,
            PerEntryPounds = perEntryPounds,
            RankTableJson = rankTableJson
        };
    }

    public void AssignToScheme(int leaguePrizeSchemeId)
    {
        Guard.Against.NegativeOrZero(leaguePrizeSchemeId);
        LeaguePrizeSchemeId = leaguePrizeSchemeId;
    }
}
