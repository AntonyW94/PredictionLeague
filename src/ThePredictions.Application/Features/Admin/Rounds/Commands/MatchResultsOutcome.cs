using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// What scoring a set of match results changed, as far as anything after the writes needs to know.
/// </summary>
/// <remarks>
/// <see cref="RoundFinished"/> is false when nothing was applied at all, which is the common case: the
/// per-minute job re-reads fixtures whose scores have not moved.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MatchResultsOutcome(bool RoundFinished);
