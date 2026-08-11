namespace ThePredictions.Domain.Services;

/// <summary>
/// How positions are awarded on every leaderboard in the application: highest score first, players on the
/// same score sharing a position, and the next player taking the position their row number implies.
///
/// So four players on 100, 90, 90 and 80 are 1st, 2nd, 2nd and 4th. **Not** 1st, 2nd, 2nd, 3rd - nobody is
/// 3rd, because two players are ahead of the player on 80. That distinction is the whole reason this is a
/// rule rather than a detail: it is the difference between the standard competition ranking every sports
/// table uses and a "dense" ranking that closes the gap, and it changes the position shown to every player
/// below a tie.
///
/// It was previously fourteen <c>RANK() OVER (ORDER BY ... DESC)</c> clauses spread across nine query
/// handlers, which is where the behaviour came from - <c>RANK()</c> awards exactly these positions, whereas
/// <c>DENSE_RANK()</c> closes the gap and <c>ROW_NUMBER()</c> ignores ties altogether. Owning it here means
/// the tie policy is one testable decision instead of fourteen copies of a SQL keyword, and it no longer
/// depends on which database is answering.
///
/// The expected positions in <c>RankingTests</c> were read off SQL Server rather than reasoned out, so they
/// are a direct check that this reproduces the behaviour being replaced.
/// </summary>
public static class Ranking
{
    /// <summary>
    /// Orders <paramref name="items"/> by <paramref name="scoreSelector"/>, highest first, and assigns each
    /// its position.
    /// </summary>
    /// <remarks>
    /// Ranking is per list, so a leaderboard split by round, month or league groups first and ranks each
    /// group - which is what <c>PARTITION BY</c> did in the SQL.
    ///
    /// Players sharing a position come back in alphabetical order of full name. That is deliberately part of
    /// the rule rather than left to each screen: it applies everywhere, so putting it here means no
    /// leaderboard can forget it and none can disagree. It is the <b>full</b> name and not the displayed
    /// "Ada L", because two players called Ada Lovelace and Ada Lamarr share a display name and so could not
    /// be ordered by it - which is precisely the case a tie-break exists to settle.
    ///
    /// Note the tie-break affects order only, never the position awarded. Ranks are compared on score alone,
    /// so alphabetical order cannot promote anyone.
    ///
    /// A score of "no result" is the caller's to define too - the SQL wrapped these keys in
    /// <c>COALESCE(..., 0)</c>, meaning a member with nothing recorded scores zero and is ranked last rather
    /// than left out. Express that in the selector.
    /// </remarks>
    public static IReadOnlyList<Ranked<T>> ByDescending<T, TKey>(
        IEnumerable<T> items,
        Func<T, TKey> scoreSelector,
        Func<T, string> fullNameSelector)
        where TKey : IComparable<TKey>
    {
        // InvariantCultureIgnoreCase rather than the ordinal comparer used for fixed internal codes: these
        // are human names, and an accented name sorting after "Z" would read as a bug.
        var ordered = items
            .OrderByDescending(scoreSelector)
            .ThenBy(fullNameSelector, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        var ranked = new List<Ranked<T>>(ordered.Count);

        var currentRank = 0;
        TKey? previousScore = default;

        for (var index = 0; index < ordered.Count; index++)
        {
            var score = scoreSelector(ordered[index]);

            // A new score takes the position its row number implies, which is what leaves a gap after a tie.
            // An equal score keeps the position already awarded.
            if (index == 0 || score.CompareTo(previousScore!) != 0)
                currentRank = index + 1;

            ranked.Add(new Ranked<T>(ordered[index], currentRank));
            previousScore = score;
        }

        return ranked;
    }
}
