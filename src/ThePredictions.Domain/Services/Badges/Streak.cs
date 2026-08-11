namespace ThePredictions.Domain.Services.Badges;

/// <summary>
/// A run of consecutive rounds a player landed something in - the rule behind the On Fire badge.
///
/// The caller supplies one flag per round, in round order, saying whether that round counted. What makes this a
/// rule rather than arithmetic is which rounds are in that sequence: every round anybody has a result for, so a
/// round the player sat out breaks the run instead of being skipped over.
/// </summary>
/// <remarks>
/// This was two gap-and-island queries, each with four chained CTEs and a pair of subtracted
/// <c>ROW_NUMBER() OVER (PARTITION BY ...)</c> windows. That technique is how a set-based language expresses a
/// run of consecutive rows, and reading one of those statements tells you nothing about what a streak is; the
/// two copies also disagreed on scope, one looking across every season and one only inside the latest, which is
/// invisible when the intent is buried in window functions.
/// </remarks>
public static class Streak
{
    /// <summary>The longest run anywhere in the sequence. Zero when nothing counted.</summary>
    public static int Longest(IEnumerable<bool> countedInRoundOrder)
    {
        var longest = 0;
        var run = 0;

        foreach (var counted in countedInRoundOrder)
        {
            run = counted ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    /// <summary>
    /// The run still going: the one that reaches the end of the sequence. Zero if the last round did not count,
    /// which is what makes this badge's second line drop back to "no current run" the moment a round is missed.
    /// </summary>
    public static int Current(IEnumerable<bool> countedInRoundOrder)
    {
        var run = 0;

        foreach (var counted in countedInRoundOrder)
            run = counted ? run + 1 : 0;

        return run;
    }
}
