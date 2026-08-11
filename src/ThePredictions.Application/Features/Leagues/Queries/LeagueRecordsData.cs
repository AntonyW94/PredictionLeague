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
/// <see cref="RoundScores"/> is <b>not</b> filtered by league membership while <see cref="ExactScores"/> is,
/// which is faithful to the old statement rather than tidy. Five of its ten blocks read
/// <c>LeagueRoundResults</c> with no membership check and two joined <c>LeagueMembers</c>, so today a player who
/// has been removed from a league can still hold its highest-round record but cannot hold its most-exact-scores
/// record. Making those agree changes what the tile shows, so it is recorded as a question for the owner rather
/// than folded into a refactor - see the plan document.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRecordsData(
    bool IsFree,
    IReadOnlyList<LeaderboardParticipantRow> ApprovedMembers,
    IReadOnlyList<LeagueRecordRoundScoreRow> RoundScores,
    IReadOnlyList<LeagueRecordExactScoreRow> ExactScores,
    IReadOnlyList<LeagueRecordWinningRow> Winnings);
