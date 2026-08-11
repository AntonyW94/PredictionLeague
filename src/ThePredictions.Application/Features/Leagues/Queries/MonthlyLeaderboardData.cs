using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind one month's leaderboard: who is in the league, what they scored in that month's rounds,
/// and the status of each of those rounds.
/// </summary>
/// <remarks>
/// <see cref="MonthRoundStatuses"/> is returned rather than the two flags the old SQL derived from it, because
/// deciding whether the month's pre-round position is worth showing is a rule - and a more particular one than
/// the overall table's. Handing over the statuses lets the handler express it and lets a test pin it.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MonthlyLeaderboardData(
    IReadOnlyList<LeaderboardMemberRow> Members,
    IReadOnlyList<MemberRoundPointsRow> RoundPoints,
    IReadOnlyList<RoundStatus> MonthRoundStatuses,
    bool HasRoundInProgress);
