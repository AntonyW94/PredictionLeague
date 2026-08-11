namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether a cached pre-round position is worth showing on an overall table, and so whether the small
/// rank-change arrow appears next to a player.
/// </summary>
/// <remarks>
/// The rank itself is read from the cache maintained on the write path under ADR-0015 - this decides only
/// whether to show it. Before any round of the season has finished there is no earlier position to have moved
/// from, so an arrow would be measuring against nothing.
///
/// Shared by the league's own overall table and the dashboard's leaderboards tile, which stated it identically
/// as <c>CASE WHEN EXISTS (... Status = @Completed) THEN stats.[SnapshotOverallRank] ELSE NULL END</c>.
/// The monthly and stage leaderboards deliberately do <b>not</b> use this: a month needs more than one of its
/// rounds started before an arrow means anything, and a stage the same within its own rounds. Three conditions
/// that look alike written as SQL, and only two of them are the same question.
/// </remarks>
public static class LeaderboardSnapshot
{
    public static int? RankToShow(int? cachedRank, bool hasCompletedRound) =>
        hasCompletedRound ? cachedRank : null;
}
