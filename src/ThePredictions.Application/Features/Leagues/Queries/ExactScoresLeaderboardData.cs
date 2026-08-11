using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a league's exact-scores leaderboard: its approved members, and each member's exact-score
/// count per round across the league's season.
/// </summary>
/// <remarks>
/// The counts are season-wide rather than league-scoped, because <c>RoundResults</c> is a global
/// per-user-per-round row. A member's total therefore includes rounds played before they joined this league -
/// existing behaviour, preserved here and written down because it is not obvious.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ExactScoresLeaderboardData(
    IReadOnlyList<LeaderboardParticipantRow> Members,
    IReadOnlyList<MemberExactScoresRow> ExactScores);
