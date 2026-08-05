namespace ThePredictions.Web.Client.Services.Time;

/// <summary>
/// Turns a day the user picked in a date input into the UTC instants that bound it, so a filter
/// on "4 August" means the 4th of August as the user's own clock reads it rather than as UTC does.
/// </summary>
public interface ILocalDayBoundaryConverter
{
    /// <summary>
    /// Midnight at the start of the picked local day, as a UTC instant. Use as an inclusive lower bound.
    /// </summary>
    Task<DateTime> StartOfDayUtcAsync(DateTime localDate);

    /// <summary>
    /// Midnight starting the local day after the one picked, as a UTC instant. Use as an
    /// <em>exclusive</em> upper bound, which is how the whole of the picked day gets included.
    /// </summary>
    Task<DateTime> StartOfNextDayUtcAsync(DateTime localDate);
}
