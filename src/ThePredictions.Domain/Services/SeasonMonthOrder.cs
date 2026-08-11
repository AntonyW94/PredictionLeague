namespace ThePredictions.Domain.Services;

/// <summary>
/// Puts a season's months in the order the season runs, rather than in calendar order.
/// </summary>
/// <remarks>
/// A season starting in August runs August to December and then January to May, so a picker listing its months
/// January-first would be useless. The rule was an <c>ORDER BY CASE WHEN ma.[Month] &gt;= si.[StartMonth] THEN 1 ELSE
/// 2 END, ma.[Month]</c> over a cross-joined CTE that existed only to work out the season's first month.
///
/// Only the month number matters, not the year, which is what the old <c>MONTH(...)</c> grouping meant too: a season
/// is assumed not to visit the same month twice.
/// </remarks>
public static class SeasonMonthOrder
{
    public static IEnumerable<T> Apply<T>(IEnumerable<T> items, Func<T, int> monthSelector, int seasonStartMonth) =>
        items
            .OrderBy(item => monthSelector(item) >= seasonStartMonth ? 1 : 2)
            .ThenBy(monthSelector);
}
