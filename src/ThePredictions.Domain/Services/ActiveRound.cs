using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// Which round of a season is the one worth showing.
/// </summary>
/// <remarks>
/// A round in play wins. Then one that finished within the last forty-eight hours, so a player checking the site the
/// morning after still sees how their round went rather than being moved straight on to next week. Then the next one
/// published and waiting. Then anything else, and in every case the lower round number first. A draft round is never
/// it, because a draft is not yet something players can see.
///
/// This was a <c>ROW_NUMBER() OVER (PARTITION BY r.[SeasonId] ORDER BY CASE ... END, r.[RoundNumber])</c> reading
/// <c>GETUTCDATE()</c>, so neither the priority order nor the grace period could be reached by a test.
///
/// <b>Two callers have to agree on this.</b> The dashboard uses it to decide which round a tile is about, and
/// <c>LeagueStatsRepository</c> uses the same order on the write path to decide which round its cached ranks belong
/// to (ADR-0015). If they ever pick different rounds, the tile shows one round's number above another round's
/// positions - a bug that looks like bad data rather than bad code. One named rule is the point; the repository
/// adopts it when the write side moves.
/// </remarks>
public static class ActiveRound
{
    /// <summary>How long a finished round keeps its place before the next one takes over.</summary>
    public static readonly TimeSpan RecentlyCompletedWindow = TimeSpan.FromHours(48);

    public static T? Of<T>(
        IEnumerable<T> rounds,
        DateTime utcNow,
        Func<T, RoundStatus> statusSelector,
        Func<T, DateTime?> completedDateUtcSelector,
        Func<T, int> roundNumberSelector)
        where T : class =>
        rounds
            .Where(round => statusSelector(round) != RoundStatus.Draft)
            .OrderBy(round => Priority(statusSelector(round), completedDateUtcSelector(round), utcNow))
            .ThenBy(roundNumberSelector)
            .FirstOrDefault();

    /// <summary>
    /// Whether a round finished recently enough to still be the one on show. A round marked complete with no
    /// completion date recorded has not, whatever its status says - there is no moment to measure the window from.
    /// </summary>
    public static bool IsRecentlyCompleted(RoundStatus status, DateTime? completedDateUtc, DateTime utcNow)
    {
        if (status != RoundStatus.Completed)
            return false;

        // Unwrapped rather than compared while still nullable: a lifted comparison on a Nullable<DateTime> carries
        // its own null branch, which the check above has already made unreachable and no test can reach.
        if (completedDateUtc is not { } completedAtUtc)
            return false;

        return completedAtUtc > utcNow - RecentlyCompletedWindow;
    }

    private static int Priority(RoundStatus status, DateTime? completedDateUtc, DateTime utcNow)
    {
        if (status == RoundStatus.InProgress)
            return 0;

        if (IsRecentlyCompleted(status, completedDateUtc, utcNow))
            return 1;

        if (status == RoundStatus.Published)
            return 2;

        return 3;
    }
}
