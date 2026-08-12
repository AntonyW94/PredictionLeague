using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>One league the player belongs to in this round's season.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PredictionLeagueRow(int LeagueId, string Name);
