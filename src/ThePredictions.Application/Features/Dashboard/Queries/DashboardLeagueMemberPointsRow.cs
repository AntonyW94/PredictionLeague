using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One member's points for one round of one league, unaggregated.
/// </summary>
/// <remarks>
/// Not <c>MemberRoundPointsRow</c> for the same reason as <see cref="DashboardLeagueMemberRow"/>: the tile totals
/// several leagues at once, so which league a row belongs to cannot be implied by the caller.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record DashboardLeagueMemberPointsRow(int LeagueId, string UserId, int BoostedPoints);
