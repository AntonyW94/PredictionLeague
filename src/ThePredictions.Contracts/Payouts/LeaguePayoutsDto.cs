using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Payouts;

/// <summary>
/// Admin view of a league's end-of-season payouts: one row per winner, with outstanding/paid totals.
/// Mark-as-paid is only available once <see cref="SeasonComplete"/> is true.
/// </summary>
[ExcludeFromCodeCoverage]
public record LeaguePayoutsDto(
    bool SeasonComplete,
    decimal OutstandingTotal,
    decimal PaidTotal,
    IReadOnlyList<LeaguePayoutWinnerDto> Winners);
