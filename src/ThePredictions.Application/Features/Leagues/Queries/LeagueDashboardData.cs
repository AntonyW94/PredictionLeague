using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a league's dashboard. Nothing is named, totalled, ordered or judged.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueDashboardData(
    LeagueDashboardHeaderRow Header,
    IReadOnlyList<LeagueDashboardRoundRow> Rounds,
    IReadOnlyList<LeagueDashboardMemberRow> Members);
