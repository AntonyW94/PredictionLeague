using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The facts behind the dashboard's leaderboards tile: the player's leagues, everyone in them, and the points
/// scored - keyed by league throughout, because this is the one leaderboard that shows several at once.
///
/// Nothing is summed, ranked, named, gated or ordered. The statement this replaced did all five inside a single
/// windowed CTE.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record DashboardLeaderboardsData(
    IReadOnlyList<DashboardLeagueRow> Leagues,
    IReadOnlyList<DashboardLeagueMemberRow> Members,
    IReadOnlyList<DashboardLeagueMemberPointsRow> Points);
