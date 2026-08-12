using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>One boost this player has already used in the season, and the round they used it in.</summary>
/// <remarks>
/// The round is here because the same rows answer two questions: what is left for the season, and what is already picked for
/// the round being predicted. Two statements asked those separately.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
/// <remarks>
/// <see cref="RoundId"/> is nullable because the column is, and deliberately so: it is set for a round-scope boost and left
/// empty for one scoped to a match or a season. The row used to declare it non-nullable, so the first boost used outside a
/// round would have made this read throw rather than return - the prediction page, for everybody in that league.
/// </remarks>
public sealed record PredictionBoostUsageRow(int LeagueId, int BoostDefinitionId, int? RoundId, string BoostCode);
