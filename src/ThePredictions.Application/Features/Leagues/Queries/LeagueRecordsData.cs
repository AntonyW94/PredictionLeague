using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Everything the records tile needs, with nothing chosen yet: who is in the league, every round score, every
/// exact-score tally and every prize awarded.
///
/// The statement this replaced picked ten winners in ten <c>OUTER APPLY</c> blocks. Picking a winner is the
/// judgement; here the rows simply arrive.
/// </summary>
/// <remarks>
/// <see cref="RoundScores"/> and <see cref="Winnings"/> arrive unfiltered by league membership;
/// <see cref="ExactScores"/> arrives already scoped to the league's approved members. That asymmetry is
/// deliberate and is <b>not</b> the population rule: <c>RoundResults</c> is league-agnostic, so reading every
/// player's whole season in order to discard most of it would be wasteful, while <c>LeagueRoundResults</c> and
/// <c>Winnings</c> are already per-league and need no narrowing.
///
/// Who may hold a record is decided once, in the handler, and narrowing at the source can only remove rows that
/// filter would remove anyway. An adapter is free to narrow the other two as well; it is free not to.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRecordsData(
    bool IsFree,
    IReadOnlyList<LeaderboardParticipantRow> ApprovedMembers,
    IReadOnlyList<LeagueRecordRoundScoreRow> RoundScores,
    IReadOnlyList<LeagueRecordExactScoreRow> ExactScores,
    IReadOnlyList<LeagueRecordWinningRow> Winnings);
