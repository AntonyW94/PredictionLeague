namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether a season has run its course.
/// </summary>
/// <remarks>
/// Three queries wrote this as a correlated subquery inside a <c>CASE</c>:
///
/// <code>
/// WHEN (SELECT COUNT(*) FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] = @CompletedStatus) >= s.[NumberOfRounds]
/// </code>
///
/// The interesting part is the <c>&gt;=</c> rather than <c>=</c>: a season carrying more completed rounds than it
/// declares is finished, not broken. That happens when rounds are added after the fact, and an equality test
/// would leave such a league showing as in play for ever.
/// </remarks>
public static class SeasonCompletion
{
    /// <summary>
    /// Finished by the season's own declared length - the definition the dashboards use.
    /// </summary>
    public static bool IsFinished(int completedRoundCount, int numberOfRounds) =>
        completedRoundCount >= numberOfRounds;

    /// <summary>
    /// Finished because every round that exists has been completed, and at least one does.
    /// </summary>
    /// <remarks>
    /// The payouts screen asks the question this way instead, and it was written as a pair of <c>EXISTS</c> clauses:
    ///
    /// <code>
    /// EXISTS (SELECT 1 FROM [Rounds] r WHERE r.[SeasonId] = l.[SeasonId])
    /// AND NOT EXISTS (SELECT 1 FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] &lt;&gt; @Completed)
    /// </code>
    ///
    /// <b>The two definitions can disagree.</b> A season declaring 38 rounds but holding 40, of which 38 are complete,
    /// is finished by <see cref="IsFinished"/> and unfinished by this one. Both are stated here rather than merged
    /// because merging them would change what one of the two screens shows, and which is right is a question for the
    /// owner - recorded in the plan document. The <c>and at least one exists</c> half is not incidental: without it an
    /// empty season would report itself finished, and the payouts screen would offer to pay out a season that has not
    /// started.
    /// </remarks>
    public static bool IsEveryRoundComplete(int roundCount, int completedRoundCount)
    {
        if (roundCount == 0)
            return false;

        return completedRoundCount == roundCount;
    }
}
