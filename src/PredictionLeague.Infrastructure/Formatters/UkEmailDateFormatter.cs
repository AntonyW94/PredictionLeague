using PredictionLeague.Application.Formatters;

namespace PredictionLeague.Infrastructure.Formatters;

public class UkEmailDateFormatter : IEmailDateFormatter
{
    private const string WindowsUkTimeZoneId = "GMT Standard Time";

    public string FormatDeadline(DateTime dateUtc)
    {
        try
        {
            var ukTimeZone = TimeZoneInfo.FindSystemTimeZoneById(WindowsUkTimeZoneId);
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