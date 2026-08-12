using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>What <see cref="IPredictionLeaguesQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PredictionLeaguesData(
    IReadOnlyList<PredictionLeagueRow> Leagues,
    IReadOnlyList<PredictionBoostRuleRow> BoostRules,
    IReadOnlyList<PredictionBoostUsageRow> BoostUsages);
