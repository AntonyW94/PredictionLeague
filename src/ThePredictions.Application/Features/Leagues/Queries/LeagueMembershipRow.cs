using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One membership of a league - approved, pending or rejected.
/// </summary>
/// <remarks>
/// Not <see cref="LeagueDashboardMemberRow"/>, which is the same shape without the user id: the management page needs it
/// to act on a request, and the dashboard has no use for it.
///
/// Name parts rather than a formatted name. The old statement selected
/// <c>FirstName + ' ' + LEFT(LastName, 1) AS FullName</c> and then ordered by that alias, so two members sharing a first
/// name and an initial were ordered arbitrarily.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueMembershipRow(
    string UserId,
    string FirstName,
    string LastName,
    DateTime JoinedAtUtc,
    LeagueMemberStatus Status);
