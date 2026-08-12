using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>One boost a league offers, and how many times a player may use it in a season.</summary>
/// <remarks>
/// Disabled rules arrive too. Whether a league counts as running boosts at all is a rule, and it is not the same question as
/// whether the player has one left.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PredictionBoostRuleRow(
    int LeagueId,
    int BoostDefinitionId,
    bool IsEnabled,
    int TotalUsesPerSeason);
