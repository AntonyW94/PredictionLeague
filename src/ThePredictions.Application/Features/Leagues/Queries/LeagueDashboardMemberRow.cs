using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Someone in the league, or waiting to be let in.
/// </summary>
/// <remarks>
/// Name parts rather than a formatted name: the dashboard shows the short form, and ordering is by first name then
/// last, so the two want different things from the same pair of columns. The old query selected
/// <c>FirstName + ' ' + LEFT(LastName, 1) AS FullName</c> - which is not a full name, and was ordered by columns it
/// had already discarded.
///
/// <see cref="Status"/> is kept because the dashboard distinguishes members from requests waiting on the
/// administrator. Which statuses appear at all is a rule and lives in the handler.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueDashboardMemberRow(
    string FirstName,
    string LastName,
    LeagueMemberStatus Status,
    DateTime JoinedAtUtc);
