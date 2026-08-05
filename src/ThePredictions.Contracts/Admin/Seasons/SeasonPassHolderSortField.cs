namespace ThePredictions.Contracts.Admin.Seasons;

/// <summary>
/// The columns the Season Pass holders list can be sorted by. A closed set, because the
/// server turns it into an ORDER BY - anything outside it falls back to the default.
/// </summary>
public enum SeasonPassHolderSortField
{
    /// <summary>The holder's full name.</summary>
    Name = 0,

    /// <summary>When the pass was acquired.</summary>
    AcquiredAt = 1,

    /// <summary>Total paid, including any SMS fee.</summary>
    TotalPaid = 2
}
