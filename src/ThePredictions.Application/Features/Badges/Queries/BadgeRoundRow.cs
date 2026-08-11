using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>
/// One round, with what the player did in it. Every round of every season comes back, because which of them count
/// towards which badge is a rule and they disagree: a streak looks at rounds anybody scored in, while ever-present
/// looks at rounds that finished.
/// </summary>
/// <remarks>
/// <see cref="UserExactScoreCount"/> is null when the player has no result for the round at all, which is not the
/// same as a result of no exact scores. The distinction is load-bearing twice over - it is how the latest season
/// they took part in is found, and a round they sat out breaks a streak rather than being skipped.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BadgeRoundRow(
    int SeasonId,
    int RoundNumber,
    RoundStatus Status,
    bool HasAnyResult,
    int? UserExactScoreCount,
    int MatchCount,
    int UserPredictionCount);
