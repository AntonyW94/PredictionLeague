namespace ThePredictions.Domain.Services;

/// <summary>
/// Picks the holder of a league record - the best, or the worst, of a set of candidates.
///
/// Ten <c>OUTER APPLY ... SELECT TOP 1 ... ORDER BY</c> blocks each did this, and between them they used four
/// different orderings for the same job.
/// </summary>
/// <remarks>
/// Every record needs the same three-part decision: the score, then a tie-break that says something (the earliest
/// round to reach a total, the earliest prize awarded), then a tie-break that says nothing but has to be
/// deterministic. Half the old blocks stopped after the first part, so which of two joint record-holders was named
/// depended on the query plan and could change between two loads of the same page.
///
/// The last part is the full name, matching <see cref="Ranking"/>: alphabetical, and never the abbreviated
/// "Ada L" form, so two players whose displayed names collide still order predictably.
/// </remarks>
public static class LeagueRecords
{
    /// <summary>The candidate with the highest score - most points, most wins, biggest prize.</summary>
    public static T? Highest<T, TScore, TTieBreak>(
        IEnumerable<T> candidates,
        Func<T, TScore> scoreSelector,
        Func<T, TTieBreak> tieBreakSelector,
        Func<T, string> fullNameSelector)
        where T : class
        where TScore : IComparable<TScore>
        where TTieBreak : IComparable<TTieBreak> =>
        candidates
            .OrderByDescending(scoreSelector)
            .ThenBy(tieBreakSelector)
            .ThenBy(fullNameSelector, StringComparer.InvariantCultureIgnoreCase)
            .FirstOrDefault();

    /// <summary>
    /// The candidate with the lowest score. Used for the one record nobody wants, so the tie-break still runs
    /// forwards: of two equally bad rounds it names the earlier one, not the later.
    /// </summary>
    public static T? Lowest<T, TScore, TTieBreak>(
        IEnumerable<T> candidates,
        Func<T, TScore> scoreSelector,
        Func<T, TTieBreak> tieBreakSelector,
        Func<T, string> fullNameSelector)
        where T : class
        where TScore : IComparable<TScore>
        where TTieBreak : IComparable<TTieBreak> =>
        candidates
            .OrderBy(scoreSelector)
            .ThenBy(tieBreakSelector)
            .ThenBy(fullNameSelector, StringComparer.InvariantCultureIgnoreCase)
            .FirstOrDefault();
}
