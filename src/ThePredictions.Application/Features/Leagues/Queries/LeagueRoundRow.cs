using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One round of a league's season, with how many fixtures it holds.
/// </summary>
/// <remarks>
/// Unordered and unfiltered, and shared by the two handlers that list a league's rounds. They want different rounds
/// from the same set - the dashboard lists all of them, the round picker only those a player may look at - so
/// filtering here would settle one of those rules on the other's behalf.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRoundRow(
    int RoundId,
    int SeasonId,
    int RoundNumber,
    string? ApiRoundName,
    DateTime StartDateUtc,
    DateTime DeadlineUtc,
    RoundStatus Status,
    int MatchCount);
