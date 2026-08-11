using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a league's overall leaderboard: who is in it, what they scored in each round, and the two
/// things about the season that decide whether a pre-round position is worth showing.
///
/// Nothing here is summed, ranked, named or ordered.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record OverallLeaderboardData(
    IReadOnlyList<LeaderboardMemberRow> Members,
    IReadOnlyList<MemberRoundPointsRow> RoundPoints,
    bool HasCompletedRound,
    bool HasRoundInProgress);
