using System.Globalization;
using Microsoft.JSInterop;

namespace ThePredictions.Web.Client.Services.Time;

public class LocalDayBoundaryConverter(IJSRuntime jsRuntime) : ILocalDayBoundaryConverter
{
    public Task<DateTime> StartOfDayUtcAsync(DateTime localDate) => ToUtcAsync(localDate.Date);

    public Task<DateTime> StartOfNextDayUtcAsync(DateTime localDate) => ToUtcAsync(localDate.Date.AddDays(1));

    /// <summary>
    /// Converts a local wall-clock time to the UTC instant it names, in the same direction as
    /// <c>UtcInputDate</c>: UTC is local plus the offset the browser reports.
    /// <para>
    /// The offset is looked up <em>at that boundary's own date</em>, not for today, so a range
    /// spanning a clock change is still correct at both ends. That is also why the timestamp is
    /// formatted without a Z suffix - JavaScript must read it as local time to report the offset
    /// that was in effect then.
    /// </para>
    /// </summary>
    private async Task<DateTime> ToUtcAsync(DateTime localMidnight)
    {
        try
        {
            var isoLocal = localMidnight.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var offsetMinutes = await jsRuntime.InvokeAsync<int>("blazorInterop.getTimezoneOffset", isoLocal);

            return localMidnight.AddMinutes(offsetMinutes);
        }
        catch
        {
            // No offset to be had, so treat the picked day as a UTC day. Wrong by at most the
            // browser's offset, which beats failing the whole filter.
            return localMidnight;
        }
    }
}
