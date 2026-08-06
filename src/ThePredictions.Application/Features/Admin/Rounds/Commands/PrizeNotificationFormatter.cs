using System.Globalization;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Presentation helpers for the "Prize Won" email: a human-readable title per prize and a money
/// string, since Brevo's template language can't compute either.
/// </summary>
public static class PrizeNotificationFormatter
{
    private static readonly CultureInfo UkCulture = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>
    /// A celebratory, self-describing title for the prize, e.g. "Gameweek 12 round winner",
    /// "November monthly winner", "Most exact scores", "Overall - 1st", "Group stage - 1st".
    /// </summary>
    public static string Title(WonPrize prize) => prize.PrizeType switch
    {
        PrizeType.Round => RoundTitle(prize),
        PrizeType.Monthly => MonthlyTitle(prize),
        PrizeType.MostExactScores => "Most exact scores",
        PrizeType.Overall => $"Overall - {DigestEmailFormatter.Ordinal(prize.Rank)}",
        PrizeType.Stages => StageTitle(prize),
        _ => FallbackTitle(prize)
    };

    /// <summary>Named rounds read better than "Round winner", but the name is not always present.</summary>
    private static string RoundTitle(WonPrize prize) =>
        string.IsNullOrWhiteSpace(prize.PrizeRoundName)
            ? "Round winner"
            : $"{prize.PrizeRoundName} round winner";

    /// <summary>Falls back to the generic title when the month is missing or out of range.</summary>
    private static string MonthlyTitle(WonPrize prize) =>
        prize.Month is >= 1 and <= 12
            ? $"{UkCulture.DateTimeFormat.GetMonthName(prize.Month.Value)} monthly winner"
            : "Monthly winner";

    private static string StageTitle(WonPrize prize) =>
        string.IsNullOrWhiteSpace(prize.Stage)
            ? $"Stage winner - {DigestEmailFormatter.Ordinal(prize.Rank)}"
            : $"{prize.Stage} - {DigestEmailFormatter.Ordinal(prize.Rank)}";

    /// <summary>An unrecognised prize type still needs a label on the email.</summary>
    private static string FallbackTitle(WonPrize prize) =>
        string.IsNullOrWhiteSpace(prize.PrizeDescription) ? "Prize" : prize.PrizeDescription!;

    /// <summary>
    /// Formats a prize amount as pounds: whole amounts drop the pence ("£10"), otherwise two
    /// decimal places ("£10.50").
    /// </summary>
    public static string Money(decimal amount)
    {
        var rounded = decimal.Round(amount, 2);

        return rounded == decimal.Truncate(rounded)
            ? $"£{rounded:0}"
            : $"£{rounded:0.00}";
    }
}
