using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>
/// One account. Both name parts arrive raw: the displayed name abbreviates the surname, and the full name is what
/// settles joint positions, so neither can be composed in the read.
/// </summary>
/// <remarks>
/// Every account comes back, including ones that never finished signing up. Whether such an account is a player is
/// a rule, and the read has no business enforcing it.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BadgePlayerRow(string UserId, string? FirstName, string? LastName);
