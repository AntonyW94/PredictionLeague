using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// The application-level inputs for evaluating a prize scheme - whether a saved
/// <see cref="LeaguePrizeScheme"/> or an unsaved draft from the create/edit editor.
/// </summary>
public sealed class PrizeSchemeEvaluationRequest
{
    public int StakePounds { get; init; }
    public int AdminTopUpPounds { get; init; }
    public int OverallFivePoundThreshold { get; init; }
    public int EntrantCount { get; init; }
    public int NumberOfRounds { get; init; }
    public int NumberOfMonths { get; init; }
    public IReadOnlyList<PrizeSchemeCategoryInput> Categories { get; init; } = [];

    /// <summary>Builds an evaluation request from a saved scheme and the live season/pot context.</summary>
    public static PrizeSchemeEvaluationRequest FromScheme(LeaguePrizeScheme scheme, int stakePounds, int entrantCount, int numberOfRounds, int numberOfMonths) => new()
    {
        StakePounds = stakePounds,
        AdminTopUpPounds = scheme.AdminTopUpPounds,
        OverallFivePoundThreshold = scheme.OverallFivePoundThreshold,
        EntrantCount = entrantCount,
        NumberOfRounds = numberOfRounds,
        NumberOfMonths = numberOfMonths,
        Categories = scheme.Entries
            .Select(e => new PrizeSchemeCategoryInput { Category = e.Category, PerEntryPounds = e.PerEntryPounds, RankTableJson = e.RankTableJson })
            .ToList()
    };
}
