using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a league's dashboard header and its member list. Nothing is named, totalled, ordered or judged.
/// </summary>
/// <remarks>
/// The rounds are not here: they come from <see cref="ILeagueRoundsQuery"/>, which the dashboard shares with the round
/// picker. One read, two callers, two different rules about which rounds to keep.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueDashboardData(
    LeagueDashboardHeaderRow Header,
    IReadOnlyList<LeagueDashboardMemberRow> Members);
