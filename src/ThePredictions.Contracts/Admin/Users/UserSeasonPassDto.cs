using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Users;

/// <summary>One Season Pass an account holds, and what was paid for it.</summary>
/// <remarks>
/// <see cref="Source"/> is the difference between a pass and a purchase. A trial or a free-season pass is still a pass -
/// it is why the account can play - but no money changed hands, so an account can hold a current pass and never have
/// paid for one.
/// </remarks>
public record UserSeasonPassDto(
    int SeasonId,
    string SeasonName,
    bool IsCurrentSeason,
    SeasonPassTier Tier,
    SeasonPassSource Source,
    decimal AmountPaid,
    decimal SmsFeePaid,
    DateTime CreatedAtUtc)
{
    /// <summary>
    /// Everything paid for this one pass.
    /// </summary>
    /// <remarks>
    /// The text-message uplift is a separate column because it is priced separately, but nobody looking at what a pass
    /// cost wants the two figures apart - and adding them at each call site is how the total on the card and the total in
    /// the popup drift.
    /// </remarks>
    public decimal TotalPaid => AmountPaid + SmsFeePaid;

    /// <summary>Whether this pass was bought, as opposed to given as a trial or granted free.</summary>
    public bool WasPurchased => Source == SeasonPassSource.Purchased;
}
