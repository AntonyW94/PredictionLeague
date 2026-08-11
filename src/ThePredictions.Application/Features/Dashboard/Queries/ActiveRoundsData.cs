using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The rounds that could appear on the dashboard and the matches in them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ActiveRoundsData(
    IReadOnlyList<ActiveRoundCandidateRow> Rounds,
    IReadOnlyList<ActiveRoundMatchRow> Matches);
