using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>One badge, one time it was earned.</summary>
/// <remarks>
/// A row per award rather than a tally per badge, because the two screens count them differently: the badges page
/// shows how many times a repeatable badge has been won, while the leaderboard counts how many different badges a
/// player holds. Grouping in the read could only serve one of them.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BadgeAwardRow(string BadgeKey, DateTime AwardedUtc);
