using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One round of a league's season: when it starts, what state it is in, and which tournament stage it belongs to.
/// </summary>
/// <remarks>
/// Not <see cref="SeasonRecapRoundRow"/>, which is otherwise the same shape without the stage text: adding a stage
/// there would leave the recap carrying a field it never reads. Not <c>SeasonRoundStageRow</c> either, which has the
/// stage text but no round number or start date, because the stage leaderboard needs neither.
///
/// <see cref="Stages"/> is null when the round has no tournament mapping at all, which is different from a mapped
/// round that is not a group round - the stage picker offers nothing for the first and a knockout stage for the second.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueSeasonRoundRow(
    int RoundId,
    int RoundNumber,
    DateTime StartDateUtc,
    RoundStatus Status,
    string? Stages);
