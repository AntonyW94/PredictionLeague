using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The facts behind the dashboard's leaderboards tile: the player's leagues, everyone in them, and what each of them
/// has scored - keyed by league throughout, because this is the one leaderboard that shows several at once.
///
/// Nothing is ranked, named, gated or ordered. The statement this replaced did all four inside a single windowed CTE,
/// along with the totalling that <see cref="DashboardLeagueMemberTotalRow"/> explains is still the database's job.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record DashboardLeaderboardsData(
    IReadOnlyList<DashboardLeagueRow> Leagues,
    IReadOnlyList<DashboardLeagueMemberRow> Members,
    IReadOnlyList<DashboardLeagueMemberTotalRow> Totals);
