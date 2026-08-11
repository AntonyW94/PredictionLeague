using ThePredictions.Domain.Models;

namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether one player may see another player's prediction for a fixture: their own always, and anyone else's
/// only once that fixture has locked.
///
/// Until August 2026 this was a <c>CASE</c> inside the league dashboard's SELECT:
///
/// <code>
/// CAST(CASE
///     WHEN COALESCE(m.[CustomLockTimeUtc], r.[DeadlineUtc]) &gt; GETUTCDATE() AND lm.[UserId] != @CurrentUserId THEN 1
///     ELSE 0
/// END AS bit) AS [IsHidden]
/// </code>
///
/// Getting it wrong shows a player what their opponents have predicted while there is still time to copy them,
/// which is the whole game.
/// </summary>
/// <remarks>
/// The near-twin of <c>BoostUsageVisibility</c>, and deliberately not shared with it. That one turns on the
/// <b>round</b> deadline because a boost is played for a round; this one turns on the <b>fixture's</b> effective
/// deadline, which a custom lock time can bring forward. Written as SQL the two predicates look like the same
/// rule with a different column, and that is exactly the resemblance this work has learned to distrust.
///
/// The lock itself is not restated here - it is <see cref="Match.IsPredictionLocked"/>, so the inclusive
/// boundary (a fixture whose deadline is exactly now has locked) is decided in one place for predicting and for
/// revealing alike. A player could otherwise be shown a prediction a tick before they could no longer change
/// their own.
/// </remarks>
public static class PredictionVisibility
{
    public static bool IsVisibleTo(
        Match match,
        string predictionOwnerUserId,
        string currentUserId,
        DateTime utcNow,
        DateTime roundDeadlineUtc)
    {
        if (predictionOwnerUserId == currentUserId)
            return true;

        return match.IsPredictionLocked(utcNow, roundDeadlineUtc);
    }
}
