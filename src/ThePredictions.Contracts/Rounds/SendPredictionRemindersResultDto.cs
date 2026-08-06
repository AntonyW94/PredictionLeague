using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Rounds;

/// <summary>
/// Outcome of an ad-hoc reminder send: how many emails went out, and how many targets were skipped
/// because they were reminded within the throttle window or no longer have any missing fixtures.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SendPredictionRemindersResultDto(
    int SentCount,
    int SkippedRecentlyRemindedCount,
    int SkippedNoLongerMissingCount);
