using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The facts behind the My Leagues tile.
///
/// The statement this replaced was the largest read on the site and the one with a performance history: it grew
/// until SQL Server spent longer planning it than running it, and the score-update job invalidated that plan about
/// once a minute, so most dashboard loads during a live round paid the full compile (ADR-0015).
/// </summary>
/// <remarks>
/// <see cref="Stats"/> is the <c>LeagueMemberStats</c> cache, read and never recomputed. That is the point of
/// ADR-0015 and nothing here changes it: the ranks are maintained on the write path, and this port fetches them by
/// key. What each rank <i>means</i> is the cache's business; which of them the tile shows, and what to show when one
/// is missing, are rules and live in the handler.
///
/// <see cref="SeasonRounds"/> carries every round of every season the player has a league in, drafts included, with
/// its match counts and stage text. That is what lets the active-round rule - the priority order that decides which
/// round the tile is about - be stated in C# instead of as a <c>ROW_NUMBER() OVER</c> with a <c>CASE</c> inside it.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MyLeaguesData(
    IReadOnlyList<MyLeagueRow> Leagues,
    IReadOnlyList<MyLeagueRoundRow> SeasonRounds,
    IReadOnlyList<MyLeagueRoundScoreRow> RoundScores,
    IReadOnlyList<MyLeagueStatsRow> Stats);
