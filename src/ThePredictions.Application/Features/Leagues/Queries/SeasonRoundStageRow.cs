using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A round in the league's season, with the tournament stage text that decides which half of the competition it
/// belongs to and its current status.
/// </summary>
/// <remarks>
/// The raw <c>Stages</c> text is carried rather than a classified stage, because classifying it is
/// <see cref="Domain.Services.TournamentStageClassifier"/>'s rule - and it was a <c>LIKE '%Group%'</c> whose
/// behaviour depended on the database collation.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonRoundStageRow(int RoundId, string? Stages, RoundStatus Status);
