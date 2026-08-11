using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Somebody waiting to be let into one of the player's leagues.
/// </summary>
/// <remarks>
/// Not <c>LeagueMembershipRow</c>, which the league's own management page uses: that one carries a status because it lists
/// memberships of every kind, and no league name because it is already about one league. This is a cross-league list of one
/// status only.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PendingMemberRow(
    int LeagueId,
    string LeagueName,
    string UserId,
    string FirstName,
    string LastName,
    DateTime JoinedAtUtc);
