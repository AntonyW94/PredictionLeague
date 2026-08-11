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

    /// <summary>
    /// How many places a player has moved up since the snapshot was taken: positive for a climb, negative for a
    /// drop, and nothing at all unless both positions are known.
    /// </summary>
    /// <remarks>
    /// The subtraction is the easy half. The rule is that a missing position on either side means no movement to
    /// report rather than a movement of zero, because those read differently to a player: zero is "you held your
    /// place", nothing is "we cannot say". The round digest email turns this into an arrow, and an arrow pointing
    /// sideways for a player who has only just joined would be a claim we cannot make.
    /// </remarks>
    public static int? PlacesGained(int? snapshotRank, int? currentRank)
    {
        if (snapshotRank is not { } from || currentRank is not { } to)
            return null;

        return from - to;
    }
}
