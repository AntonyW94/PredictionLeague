namespace ThePredictions.Domain.Services;

/// <summary>
/// When a round's predictions close, and whether one match's have.
///
/// The row-level twin of the rules on <see cref="Models.Round"/> and <see cref="Models.Match"/>, for the read paths that
/// have match rows rather than entities.
/// </summary>
public static class PredictionWindow
{
    /// <summary>
    /// The latest moment any match in the round can still be predicted: the round's own deadline, unless a match carries a
    /// custom lock time later than it.
    /// </summary>
    /// <remarks>
    /// A combined round - World Cup semi-finals alongside the final, say - stays open for its later matches after the round
    /// deadline that locked the earlier ones has passed.
    ///
    /// <b>The caller decides which matches count, and that matters.</b> The dashboard passes only the matches that have not
    /// been postponed, because a postponed match cannot be predicted and so should not hold a round open. That is what the
    /// old SQL did, with <c>lm.[Status] &lt;&gt; @PostponedStatus</c> inside its <c>MAX</c>.
    ///
    /// <c>Round.GetLatestPredictionDeadline</c> answers the same question over <b>every</b> match including postponed ones,
    /// so the two disagree for a round holding a postponed match with a late custom lock. Its sibling
    /// <c>Round.GetNextPredictionDeadline</c> excludes postponed matches, so the entity is inconsistent with itself. Recorded
    /// in the plan document rather than changed here, because the entity method has other callers.
    /// </remarks>
    public static DateTime LatestDeadline(DateTime roundDeadlineUtc, IEnumerable<DateTime?> matchLockTimesUtc)
    {
        var latest = roundDeadlineUtc;

        foreach (var lockTime in matchLockTimesUtc)
        {
            if (lockTime is { } lockedAt && lockedAt > latest)
                latest = lockedAt;
        }

        return latest;
    }

    /// <summary>
    /// Whether one match's predictions have closed - its own lock time if it has one, otherwise the round's deadline.
    /// </summary>
    /// <remarks>
    /// The same rule and the same inclusive boundary as <c>Match.IsPredictionLocked</c>: a match whose deadline is exactly now
    /// has locked. Stated for rows as well as entities because the dashboard uses it to decide when a match's prediction split
    /// may be shown, and revealing that early would show players what their opponents have chosen while they can still copy it.
    /// </remarks>
    public static bool HasLocked(DateTime? customLockTimeUtc, DateTime roundDeadlineUtc, DateTime utcNow) =>
        (customLockTimeUtc ?? roundDeadlineUtc) <= utcNow;
}
