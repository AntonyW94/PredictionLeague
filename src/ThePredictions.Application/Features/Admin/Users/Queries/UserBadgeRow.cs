using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One badge an account has earned.</summary>
/// <remarks>
/// The key arrives, not a name. Badge names live in the catalogue in code, so the database has never stored one and this
/// read must not invent one. <see cref="SeasonId"/> is null for a lifetime badge, which is not scoped to a season at all.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserBadgeRow(
    string UserId,
    string BadgeKey,
    string? Detail,
    DateTime AwardedUtc,
    int? SeasonId);
