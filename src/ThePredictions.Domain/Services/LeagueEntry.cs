namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether a league is still accepting entries.
/// </summary>
/// <remarks>
/// Both league-discovery queries expressed this as <c>l.[EntryDeadlineUtc] &gt; GETUTCDATE()</c>, which did two things at
/// once. The obvious one is the comparison. The subtle one is that <c>NULL &gt; anything</c> is unknown in SQL, so a league
/// with <b>no</b> entry deadline was silently never joinable - a rule nobody wrote down, enforced by three-valued logic.
///
/// Stated here so it survives being read by someone who does not think in SQL nulls, and so the comparison can be made
/// against an injected clock rather than the database's.
/// </remarks>
public static class LeagueEntry
{
    public static bool IsOpen(DateTime? entryDeadlineUtc, DateTime utcNow)
    {
        if (entryDeadlineUtc is not { } deadline)
            return false;

        return deadline > utcNow;
    }
}
