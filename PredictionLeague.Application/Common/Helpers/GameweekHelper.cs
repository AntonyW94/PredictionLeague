namespace PredictionLeague.Application.Common.Helpers;

public static class GameweekHelper
{
    private const DayOfWeek GameweekStartDay = DayOfWeek.Wednesday;

    public static DateTime GetGameweekStartDate(DateTime date)
    {
        var daysToAdd = GameweekStartDay - date.DayOfWeek;
        if (daysToAdd > 0)
            daysToAdd -= 7;
        
        return date.Date.AddDays(daysToAdd);
    }
}