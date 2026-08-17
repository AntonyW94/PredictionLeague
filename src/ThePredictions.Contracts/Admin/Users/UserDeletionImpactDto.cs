namespace ThePredictions.Contracts.Admin.Users;

/// <summary>
/// What deleting one account would destroy, for the confirmation dialog to state before an administrator
/// commits to it.
/// </summary>
/// <remarks>
/// <see cref="LeaguesAdministered"/> is not part of the destruction: those leagues survive the account and
/// have to be handed to a new administrator first. It travels here because the same screen asks both
/// questions at the same moment.
/// </remarks>
public record UserDeletionImpactDto(
    int SeasonPasses,
    decimal SeasonPassSpend,
    int LeagueMemberships,
    int Predictions,
    int Winnings,
    decimal WinningsTotal,
    int Payouts,
    decimal PayoutsTotal,
    int Badges,
    int BoostUsages,
    int RoundResults,
    int LeagueRoundResults,
    int LeagueStandings,
    int EmailRecords,
    int OnboardingSkips,
    bool HasPayoutDetails,
    int LeaguesAdministered
)
{
    /// <summary>
    /// How many records the delete would destroy, across every category.
    /// </summary>
    /// <remarks>
    /// Administered leagues are excluded: they are re-assigned rather than deleted, so counting them here
    /// would overstate the damage.
    /// </remarks>
    public int TotalRecords =>
        SeasonPasses
        + LeagueMemberships
        + Predictions
        + Winnings
        + Payouts
        + Badges
        + BoostUsages
        + RoundResults
        + LeagueRoundResults
        + LeagueStandings
        + EmailRecords
        + OnboardingSkips;

    /// <summary>Whether the account has any history at all, as opposed to being an empty registration.</summary>
    /// <remarks>
    /// A sum rather than a chain of twelve <c>|| x > 0</c> comparisons. Identical in meaning, because a count
    /// cannot be negative - and the chain was the whole report's only risk hotspot at a cyclomatic complexity
    /// of 24, which for "is any of this non-zero" is complexity that says nothing.
    /// </remarks>
    public bool HasAnyRecords => TotalRecords > 0 || HasPayoutDetails;

    /// <summary>
    /// Whether money is involved, which is the part of a deletion that cannot be reconstructed from
    /// anywhere else and is worth calling out separately in the dialog.
    /// </summary>
    public bool HasFinancialRecords => SeasonPasses > 0 || Winnings > 0 || Payouts > 0 || HasPayoutDetails;
}
