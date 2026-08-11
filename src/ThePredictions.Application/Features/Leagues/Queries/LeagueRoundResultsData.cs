using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Everything the league dashboard's round grid needs, uninterpreted: the round with its fixtures, the members
/// who could have predicted them, the predictions that exist, the points scored and the boosts played.
///
/// Nothing is ranked, nothing is hidden and nothing is defaulted. The old statement did all three at once, in a
/// <c>CROSS JOIN</c> that produced a row per member per fixture and then re-grouped them back in C#.
/// </summary>
/// <remarks>
/// <see cref="Round"/> is the domain entity rather than a deadline and a list of fixture rows, for the same
/// reason <c>RoundCompletionData</c> carries one: whether a prediction may be revealed is
/// <c>PredictionVisibility.IsVisibleTo</c> over <c>Match.IsPredictionLocked</c>, and whether a fixture belongs on
/// the grid is <c>Match.IsPostponed</c>. Flat rows would force both to be restated against their fields.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRoundResultsData(
    Round Round,
    IReadOnlyList<LeaderboardParticipantRow> Members,
    IReadOnlyList<MemberPredictionRow> Predictions,
    IReadOnlyList<MemberRoundPointsRow> Points,
    IReadOnlyList<MemberBoostUsageRow> BoostUsages);
