using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One player an administrator could place in a league.
/// </summary>
/// <remarks>
/// Name parts rather than a formatted name, for the same reason as <see cref="LeagueMembershipRow"/>: the dropdown is
/// ordered by first name then last name, and ordering by an abbreviated "Ada L" cannot separate two people who both
/// render that way.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueJoinCandidateRow(
    string UserId,
    string FirstName,
    string LastName,
    string Email);
