namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether a season has run its course, which is what marks a league as finished on the dashboard.
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
    public static bool IsFinished(int completedRoundCount, int numberOfRounds) =>
        completedRoundCount >= numberOfRounds;
}
