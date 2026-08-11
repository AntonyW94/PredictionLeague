namespace ThePredictions.Domain.Services;

/// <summary>
/// Who won each period of a league - a round, a calendar month, anything the caller groups by.
/// </summary>
/// <remarks>
/// This was four <c>RANK() OVER (PARTITION BY ...)</c> windows across two handlers: the records tile asked who had
/// won the most rounds and months, and the season recap asked how many one player had won. The same rule answering
/// two questions, so it is stated once here and each caller counts what it needs.
///
/// Two parts of it are easy to lose:
///
/// <list type="bullet">
/// <item>Joint winners both win. That is why the SQL used <c>RANK</c> rather than <c>ROW_NUMBER</c>, and it is why
/// this returns one entry per winner rather than one per period.</item>
/// <item>A period nobody scored in is won by nobody. Without that, a round created but not yet scored hands every
/// member of the league a win.</item>
/// </list>
/// </remarks>
public static class Wins
{
    /// <summary>
    /// The winners, one entry per win - so a player who won three rounds appears three times. Points are totalled
    /// per player within each period first, which is what makes a month's winner the best over its rounds rather
    /// than the winner of its best round.
    /// </summary>
    public static IReadOnlyList<string> ByPeriod<T, TPeriod>(
        IEnumerable<T> rows,
        Func<T, TPeriod> periodSelector,
        Func<T, string> userIdSelector,
        Func<T, int> pointsSelector)
        where TPeriod : notnull
    {
        var totals = rows
            .GroupBy(row => (Period: periodSelector(row), UserId: userIdSelector(row)))
            .Select(group => (group.Key.Period, group.Key.UserId, Points: group.Sum(pointsSelector)))
            .ToList();

        var winners = new List<string>();

        foreach (var period in totals.GroupBy(total => total.Period))
        {
            var best = period.Max(total => total.Points);

            if (best <= 0)
                continue;

            winners.AddRange(period.Where(total => total.Points == best).Select(total => total.UserId));
        }

        return winners;
    }
}
