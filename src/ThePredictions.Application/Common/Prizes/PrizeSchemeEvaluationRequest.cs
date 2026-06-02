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
    public int OverallRoundingThresholdPounds { get; init; }
    public int EntrantCount { get; init; }
    public int NumberOfRounds { get; init; }
    public int NumberOfMonths { get; init; }
    public IReadOnlyList<PrizeSchemeCategoryInput> Categories { get; init; } = [];

    /// <summary>
    /// Builds an evaluation request from a saved scheme and the live season/pot context. The admin
    /// top-up comes from the league's <see cref="League.PrizeFundOverride"/>, not the scheme.
    /// </summary>
    public static PrizeSchemeEvaluationRequest FromScheme(LeaguePrizeScheme scheme, int stakePounds, int adminTopUpPounds, int entrantCount, int numberOfRounds, int numberOfMonths) => new()
    {
        StakePounds = stakePounds,
        AdminTopUpPounds = adminTopUpPounds,
        OverallRoundingThresholdPounds = scheme.OverallRoundingThresholdPounds,
        EntrantCount = entrantCount,
        NumberOfRounds = numberOfRounds,
        NumberOfMonths = numberOfMonths,
        Categories = scheme.Entries
            .Select(e => new PrizeSchemeCategoryInput { Category = e.Category, PerEntryPounds = e.PerEntryPounds, RankTableJson = e.RankTableJson })
            .ToList()
    };
}
