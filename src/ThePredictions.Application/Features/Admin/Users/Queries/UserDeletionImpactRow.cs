using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// How much of one account's history a delete would destroy, counted per kind of record.
/// </summary>
/// <remarks>
/// Every count here is a row that <b>goes</b> when the account goes - either because its foreign key
/// cascades or because <c>0009_CascadeUserDeletion.sql</c> made it cascade. <see cref="LeaguesAdministered"/>
/// is the exception and the reason this type is not simply a total: those leagues do <i>not</i> go, they have
/// to be handed to somebody else first, so the screen has to ask a different question about them.
///
/// Counts rather than the rows themselves. The confirmation dialog states magnitudes, and pulling a
/// player's whole prediction history back to say "412" would be a strange way to say it.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserDeletionImpactRow(
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
    int LeaguesAdministered);
