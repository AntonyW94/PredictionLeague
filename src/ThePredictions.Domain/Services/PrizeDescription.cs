using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// How a prize is labelled on the records tile: the admin's own wording if they gave any, otherwise one derived
/// from what the prize was for.
/// </summary>
/// <remarks>
/// This was a three-armed <c>CASE</c> ending in <c>DATENAME(MONTH, DATEFROMPARTS(2000, w.[Month], 1))</c> - a
/// month name produced by the database, and therefore by whatever language the SQL Server login happens to be
/// configured with. Two identical databases could label the same prize "March" and "marzo". Formatting a month
/// name is presentation, it was never the database's job, and here it is pinned to the invariant culture.
///
/// The empty check mirrors the old <c>&lt;&gt; ''</c> exactly, which is subtler than it looks: SQL Server ignores
/// trailing spaces when comparing strings, so a description of "   " counted as empty there. That makes
/// <c>IsNullOrWhiteSpace</c> the faithful translation and <c>IsNullOrEmpty</c> a behaviour change.
/// </remarks>
public static class PrizeDescription
{
    public static string? For(string? adminDescription, PrizeType prizeType, int? roundNumber, int? month)
    {
        if (!string.IsNullOrWhiteSpace(adminDescription))
            return adminDescription;

        if (prizeType == PrizeType.Round)
            return $"Round {roundNumber}";

        if (prizeType == PrizeType.Monthly)
            return MonthName.Of(month);

        return null;
    }
}
