using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>One badge, one time it was earned, and who by.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BadgePlayerAwardRow(string UserId, string BadgeKey, DateTime AwardedUtc);
