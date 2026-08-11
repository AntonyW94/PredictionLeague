namespace ThePredictions.Domain.Services;

/// <summary>
/// What a league's prize pot is worth, and what is left of it.
/// </summary>
/// <remarks>
/// The pot is every member's entry fee plus whatever the administrator has put in on top. That formula was written
/// out in SQL in two places - the My Leagues tile and the available-leagues list - as
/// <c>l.[Price] * memberCount + ISNULL(l.[PrizeFundOverride], 0)</c>.
///
/// A free league has a price of zero, so the same formula gives a pot of nothing unless the administrator has
/// funded one, which is the intended behaviour rather than a special case.
/// </remarks>
public static class PrizeFund
{
    public static decimal Total(decimal entryFee, int memberCount, decimal? administratorTopUp) =>
        entryFee * memberCount + (administratorTopUp ?? 0m);

    public static decimal Remaining(decimal total, decimal alreadyPaidOut) =>
        total - alreadyPaidOut;
}
