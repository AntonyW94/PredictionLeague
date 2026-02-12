namespace PredictionLeague.Web.Client.Utilities;

public static class FormattingUtilities
{
    public static string GetOrdinal(int num)
    {
        if (num <= 0)
            return "";
        
        return (num % 100) switch
        {
            11 or 12 or 13 => "th",
            _ => (num % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            }
        };
    }
}