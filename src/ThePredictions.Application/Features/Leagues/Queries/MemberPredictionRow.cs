using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One prediction that exists, with its scores and how it scored.
/// </summary>
/// <remarks>
/// Distinct from <c>RoundPredictionRow</c>, which is a bare (player, fixture) pair because completion only counts
/// predictions rather than reading them.
///
/// <see cref="Outcome"/> is not nullable: the column is <c>NOT NULL DEFAULT 0</c>, so the old
/// <c>ISNULL(up.[Outcome], 0)</c> was never guarding a null column - it was filling in the fixtures a member had
/// not predicted, which the <c>CROSS JOIN</c> had manufactured rows for. Those cells are now the handler's job,
/// so absent means absent here.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MemberPredictionRow(
    string UserId,
    int MatchId,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    PredictionOutcome Outcome);
