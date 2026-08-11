using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's name and its memberships, in whatever order they came back.
/// </summary>
/// <remarks>
/// Every membership, including the ones that were turned away: this is the administrator's own management page and
/// seeing a rejected request is the point of it. That is a different question from the league dashboard, which lists
/// members and pending requests but not rejections.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueMembersData(string LeagueName, IReadOnlyList<LeagueMembershipRow> Members);
