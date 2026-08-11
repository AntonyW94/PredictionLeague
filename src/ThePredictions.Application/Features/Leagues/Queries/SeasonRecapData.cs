using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind one player's season recap. Nothing is ranked, averaged, totalled or counted.
///
/// The statement this replaced held four <c>RANK() OVER</c> windows, one of them over a running total computed
/// round by round across the whole league.
/// </summary>
/// <remarks>
/// <see cref="SeasonRounds"/> carries every round of the season, not only the ones the player scored in, and not
/// only the completed ones. Both matter: the position trajectory steps through completed rounds even where nobody
/// scored, while the player's best and worst rounds are drawn from every round they have a result for whatever its
/// status - which is what the old statement did, in two blocks that filtered differently.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonRecapData(
    bool IsFree,
    decimal LeaguePrice,
    IReadOnlyList<LeaderboardParticipantRow> ApprovedMembers,
    IReadOnlyList<SeasonRecapRoundRow> SeasonRounds,
    IReadOnlyList<MemberRoundPointsByRoundRow> RoundScores,
    IReadOnlyList<int> ExactScoreCounts,
    IReadOnlyList<decimal> WinningAmounts);
