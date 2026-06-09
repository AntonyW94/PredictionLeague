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
        PrizeType.Round => string.IsNullOrWhiteSpace(prize.PrizeRoundName)
            ? "Round winner"
            : $"{prize.PrizeRoundName} round winner",
        PrizeType.Monthly => prize.Month is >= 1 and <= 12
            ? $"{UkCulture.DateTimeFormat.GetMonthName(prize.Month.Value)} monthly winner"
            : "Monthly winner",
        PrizeType.MostExactScores => "Most exact scores",
        PrizeType.Overall => $"Overall - {DigestEmailFormatter.Ordinal(prize.Rank)}",
        PrizeType.Stages => string.IsNullOrWhiteSpace(prize.Stage)
            ? $"Stage winner - {DigestEmailFormatter.Ordinal(prize.Rank)}"
            : $"{prize.Stage} - {DigestEmailFormatter.Ordinal(prize.Rank)}",
        _ => string.IsNullOrWhiteSpace(prize.PrizeDescription) ? "Prize" : prize.PrizeDescription!
    };

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
