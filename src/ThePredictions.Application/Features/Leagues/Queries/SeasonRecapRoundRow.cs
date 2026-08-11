using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One round of the league's season, with the two things the recap's rules need to know about it: whether it has
/// finished, and when it started.
/// </summary>
/// <remarks>
/// Not <c>SeasonRoundStageRow</c>, which carries the tournament stage text and no round number: that one exists so
/// the stage leaderboard can classify a round, and this one so the recap can order rounds and place them in a
/// calendar month. Same table, different questions.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonRecapRoundRow(
    int RoundId,
    int RoundNumber,
    DateTime StartDateUtc,
    RoundStatus Status);
