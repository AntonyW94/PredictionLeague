using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a tournament stage's leaderboard: the league's approved members, every round in the season
/// with its stage text and status, and every member's points per round.
/// </summary>
/// <remarks>
/// Deliberately not narrowed to one stage. Which rounds belong to the requested stage is a classification rule,
/// so the port returns them all and the handler decides - otherwise the <c>LIKE '%Group%'</c> would simply have
/// moved from one SQL statement to another.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record StageLeaderboardData(
    IReadOnlyList<LeaderboardParticipantRow> Members,
    IReadOnlyList<SeasonRoundStageRow> SeasonRounds,
    IReadOnlyList<MemberRoundPointsByRoundRow> RoundPoints,
    bool HasRoundInProgress);
