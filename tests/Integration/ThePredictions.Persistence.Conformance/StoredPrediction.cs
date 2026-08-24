using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.Conformance;

/// <summary>One player's stored prediction for one fixture, as the database holds it.</summary>
/// <remarks>
/// <c>UpdatedAtUtc</c> is here because the write path sets it from the injected clock rather than from the
/// database's, and a set-based rewrite of that write is exactly the kind of change that could quietly go back
/// to <c>GETUTCDATE()</c> without any other assertion noticing.
/// </remarks>
public sealed record StoredPrediction(
    int PredictedHomeScore,
    int PredictedAwayScore,
    PredictionOutcome Outcome,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
