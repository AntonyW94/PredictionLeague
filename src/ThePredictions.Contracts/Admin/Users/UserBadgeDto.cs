using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

/// <summary>One badge an account has earned.</summary>
/// <remarks>
/// <see cref="Name"/> is resolved from the badge catalogue rather than stored, and falls back to the stored key for a
/// badge the catalogue no longer defines. <see cref="SeasonName"/> is null for a lifetime badge, which is not scoped to
/// a season at all.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UserBadgeDto(
    string BadgeKey,
    string Name,
    string? Detail,
    DateTime AwardedUtc,
    int? SeasonId,
    string? SeasonName);
