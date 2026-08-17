using ThePredictions.Contracts.Admin.Users;

namespace ThePredictions.Web.Client.ViewModels.Admin.Users;

/// <summary>
/// Turns the counts behind a pending account deletion into the lines the confirmation dialog reads out.
/// </summary>
/// <remarks>
/// A separate class rather than a method on the page because this is the wording of an irreversible
/// warning, and getting it wrong - "1 season passes", or a category silently omitted - is the kind of
/// mistake that only shows up in front of an administrator about to delete somebody's history. Logic in a
/// <c>.razor</c> file cannot be unit tested; this can.
///
/// Only non-empty categories appear. A list padded with "0 badges" buries the two lines that matter,
/// and the dialog has a separate empty case for an account with no history at all.
/// </remarks>
public static class UserDeletionImpactSummary
{
    /// <summary>
    /// One line per kind of record the account holds, most consequential first: money, then participation,
    /// then the incidental rows nobody will miss.
    /// </summary>
    public static IReadOnlyList<string> Describe(UserDeletionImpactDto impact)
    {
        var lines = new List<string>();

        // Money first. These are the records that exist nowhere else once they are gone.
        Add(lines, impact.SeasonPasses, "season pass", "season passes", Paid(impact.SeasonPassSpend));
        Add(lines, impact.Winnings, "prize win", "prize wins", Totalling(impact.WinningsTotal));
        Add(lines, impact.Payouts, "recorded payout", "recorded payouts", Totalling(impact.PayoutsTotal));

        if (impact.HasPayoutDetails)
            lines.Add("Their saved bank details");

        // Participation.
        Add(lines, impact.LeagueMemberships, "league membership", "league memberships");
        Add(lines, impact.Predictions, "prediction", "predictions");
        Add(lines, impact.BoostUsages, "boost played", "boosts played");
        Add(lines, impact.Badges, "badge earned", "badges earned");

        // Derived and incidental.
        Add(lines, impact.LeagueRoundResults, "league round score", "league round scores");
        Add(lines, impact.RoundResults, "overall round result", "overall round results");
        Add(lines, impact.LeagueStandings, "league standing", "league standings");
        Add(lines, impact.EmailRecords, "email record", "email records");
        Add(lines, impact.OnboardingSkips, "dismissed onboarding tip", "dismissed onboarding tips");

        return lines;
    }

    private static void Add(List<string> lines, int count, string singular, string plural, string? suffix = null)
    {
        if (count == 0)
            return;

        var noun = count == 1 ? singular : plural;

        lines.Add(suffix is null
            ? $"{count:N0} {noun}"
            : $"{count:N0} {noun} {suffix}");
    }

    /// <summary>
    /// Omitted entirely when nothing was paid, because "1 season pass (£0.00 paid)" reads as a billing bug
    /// rather than as the trial or comped pass it actually is.
    /// </summary>
    private static string? Paid(decimal amount) => amount > 0 ? $"({FormatMoney(amount)} paid)" : null;

    private static string? Totalling(decimal amount) => amount > 0 ? $"totalling {FormatMoney(amount)}" : null;

    /// <summary>Whole pounds without a decimal part, matching the money already on the user list.</summary>
    private static string FormatMoney(decimal amount) => amount % 1 == 0
        ? $"£{amount:N0}"
        : $"£{amount:N2}";
}
