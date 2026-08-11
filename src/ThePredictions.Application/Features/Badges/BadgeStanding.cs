using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Services.Badges;

namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// Where one player stands on the badges table, before a position is put against it. The full name is carried
/// alongside the displayed one because that is what settles a joint position, and "Ada L" cannot do that job.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
internal sealed record BadgeStanding(
    string UserId,
    string DisplayName,
    string FullName,
    BadgeTally Tally);
