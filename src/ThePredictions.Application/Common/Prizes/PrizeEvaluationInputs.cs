namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// The live inputs needed to evaluate a league's prize scheme: pot context (stake, entrant count,
/// admin top-up), season event counts, the scheme entries, and headline league facts.
/// </summary>
public sealed class PrizeEvaluationInputs
{
    public int LeagueId { get; init; }
    public string LeagueName { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public string AdministratorName { get; init; } = string.Empty;
    public string AdministratorUserId { get; init; } = string.Empty;
    public string? EntryCode { get; init; }
    public decimal EntryCost { get; init; }
    public int EntrantCount { get; init; }
    public DateTime EntryDeadlineUtc { get; init; }
    public int NumberOfRounds { get; init; }
    public int NumberOfMonths { get; init; }

    public bool HasScheme { get; init; }
    public int AdminTopUpPounds { get; init; }
    public IReadOnlyList<PrizeSchemeCategoryInput> Categories { get; init; } = [];

    public bool IsPrivate => !string.IsNullOrEmpty(EntryCode);

    /// <summary>Builds an evaluation request at the given entrant count (whole-pound stake).</summary>
    public PrizeSchemeEvaluationRequest ToEvaluationRequest(int entrantCount) => new()
    {
        StakePounds = (int)decimal.Truncate(EntryCost),
        AdminTopUpPounds = AdminTopUpPounds,
        EntrantCount = entrantCount,
        NumberOfRounds = NumberOfRounds,
        NumberOfMonths = NumberOfMonths,
        Categories = Categories
    };
}
