namespace ThePredictions.Application.Formatters;

// Derives the urgency tier and a human-readable countdown for the prediction-reminder email from the
// time left until the deadline. Calculated server-side and passed to Brevo as simple strings so the
// template can switch on URGENCY without float comparisons (see the prediction-reminders plan).
public static class ReminderUrgencyFormatter
{
    private static readonly TimeSpan SoonThreshold = TimeSpan.FromHours(6);
    private static readonly TimeSpan RelaxedThreshold = TimeSpan.FromHours(24);

    // "urgent" (< 6h left), "soon" (6-24h left) or "relaxed" (24h+ left).
    public static string GetUrgencyTier(TimeSpan timeRemaining)
    {
        if (timeRemaining < SoonThreshold)
            return "urgent";

        if (timeRemaining < RelaxedThreshold)
            return "soon";

        return "relaxed";
    }

    // A rounded-down, human-readable countdown: "3 days", "6 hours", "45 minutes". Anything under a
    // minute (including a deadline that has just passed) reads as "less than a minute".
    public static string FormatTimeRemaining(TimeSpan timeRemaining)
    {
        if (timeRemaining < TimeSpan.FromMinutes(1))
            return "less than a minute";

        if (timeRemaining.TotalDays >= 1)
            return Pluralise((int)timeRemaining.TotalDays, "day");

        if (timeRemaining.TotalHours >= 1)
            return Pluralise((int)timeRemaining.TotalHours, "hour");

        return Pluralise((int)timeRemaining.TotalMinutes, "minute");
    }

    private static string Pluralise(int value, string unit) =>
        value == 1 ? $"1 {unit}" : $"{value} {unit}s";
}
