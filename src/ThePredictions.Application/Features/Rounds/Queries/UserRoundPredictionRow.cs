using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>One prediction a player has made, and how it turned out.</summary>
/// <remarks>
/// The scores are nullable because the row exists from the moment a player starts filling the form in. The outcome is
/// <see cref="PredictionOutcome.Pending"/> until the fixture is scored.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserRoundPredictionRow(
    int MatchId,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    PredictionOutcome Outcome);
