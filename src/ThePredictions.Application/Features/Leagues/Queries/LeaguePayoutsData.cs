using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a league's payout screen.
/// </summary>
/// <remarks>
/// <see cref="SeasonRoundCount"/> and <see cref="CompletedRoundCount"/> arrive as counts rather than as a
/// season-complete flag. This screen defines a finished season as "every round that exists is complete", which is not
/// the definition the dashboards use - see <c>SeasonCompletion</c>, where both now live side by side with a note that
/// they can disagree.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaguePayoutsData(
    bool IsAdministrator,
    int SeasonRoundCount,
    int CompletedRoundCount,
    IReadOnlyList<PayoutWinningRow> Winnings,
    IReadOnlyList<StoredPayoutRow> StoredPayouts,
    IReadOnlyList<PayoutBankDetailsRow> BankDetails);
