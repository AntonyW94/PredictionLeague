using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>One boost this player has already used in the season, and the round they used it in.</summary>
/// <remarks>
/// The round is here because the same rows answer two questions: what is left for the season, and what is already picked for
/// the round being predicted. Two statements asked those separately.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PredictionBoostUsageRow(int LeagueId, int BoostDefinitionId, int RoundId, string BoostCode);
