namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether a season has run its course: every round it holds has been completed, and it holds at least one.
/// </summary>
/// <remarks>
/// There were two definitions of this, and they could disagree. The dashboards asked whether the number of completed
/// rounds had reached the number the season <b>declares</b>:
///
/// <code>
/// (SELECT COUNT(*) FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] = @Completed) &gt;= s.[NumberOfRounds]
/// </code>
///
/// while the payouts screen asked whether every round that <b>exists</b> had been completed. A season declaring 38 rounds
/// but holding 40, of which 38 are complete, was finished by the first and unfinished by the second.
///
/// This is the second, and now the only one. The declared length is a number an administrator typed and the football API
/// can add rounds beyond it, so the rounds that exist are the better authority - and the failure modes are not
/// symmetrical. Reaching the declared count too early would let the payouts screen offer to settle a season with rounds
/// still to play; requiring every round to be complete can only ever be too cautious, which for money is the right way
/// round.
///
/// The <c>at least one round exists</c> half is not incidental. Without it an empty season reports itself finished, and
/// the payouts screen offers to pay out a season that has not started.
/// </remarks>
public static class SeasonCompletion
{
    /// <remarks>
    /// The two counts are interchangeable for every input a database can hold - a season cannot have completed rounds
    /// without having rounds - so swapping them at a call site changes no answer, and no test can catch it. Every caller
    /// therefore passes them by name, which is what protects a reader instead.
    /// </remarks>
    public static bool IsEveryRoundComplete(int roundCount, int completedRoundCount)
    {
        if (roundCount == 0)
            return false;

        return completedRoundCount == roundCount;
    }
}
