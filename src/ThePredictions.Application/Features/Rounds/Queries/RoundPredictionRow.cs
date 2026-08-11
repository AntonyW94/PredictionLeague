using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// One prediction that exists, as a (player, fixture) pair. Which of these count towards completion is
/// decided in C#, so the port returns them all rather than a filtered count.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundPredictionRow(string UserId, int MatchId);
