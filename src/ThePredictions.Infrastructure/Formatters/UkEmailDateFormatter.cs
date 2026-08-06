using ThePredictions.Application.Formatters;

namespace ThePredictions.Infrastructure.Formatters;

public class UkEmailDateFormatter : IEmailDateFormatter
{
    private const string WindowsUkTimeZoneId = "GMT Standard Time";

    private readonly string _timeZoneId;

    public UkEmailDateFormatter() : this(WindowsUkTimeZoneId)
    {
    }

    /// <summary>
    /// Test seam. The UTC fallback below only runs on a host with no UK time zone data, which
    /// cannot be arranged from a test any other way.
    /// </summary>
    internal UkEmailDateFormatter(string timeZoneId) => _timeZoneId = timeZoneId;

    public string FormatDeadline(DateTime dateUtc)
    {
        try
        {
            var ukTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
            var ukDate = TimeZoneInfo.ConvertTimeFromUtc(dateUtc, ukTimeZone);
            var suffix = ukTimeZone.IsDaylightSavingTime(ukDate) ? "BST" : "GMT";

            return $"{ukDate:dddd, dd MMMM yyyy 'at' HH:mm} ({suffix})";
        }
        catch (TimeZoneNotFoundException)
        {
            return $"{dateUtc:dddd, dd MMMM yyyy 'at' HH:mm} (UTC)";
        }
    }
}