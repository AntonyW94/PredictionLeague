using System.Globalization;

namespace ThePredictions.Domain.Services;

/// <summary>
/// A month number as its English name.
/// </summary>
/// <remarks>
/// Two queries asked the database for this - <c>DATENAME(MONTH, ...)</c> - which means the name came out in
/// whatever language the SQL Server login happened to be configured with. Two identical databases could label the
/// same month "March" and "marzo". Pinned to the invariant culture here.
///
/// Returns nothing for a month outside 1-12 rather than throwing, which is what <c>DATEFROMPARTS</c> did: the
/// month number on a prize is a stored value, and one bad row should not take a whole page down.
/// </remarks>
public static class MonthName
{
    public static string? Of(int? month)
    {
        if (month is null or < 1 or > 12)
            return null;

        return CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month.Value);
    }
}
