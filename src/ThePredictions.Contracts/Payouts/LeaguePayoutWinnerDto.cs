using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Payouts;

/// <summary>
/// One winner's aggregated payout: total, live breakdown, settlement state and shared bank details (if any).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeaguePayoutWinnerDto(
    string UserId,
    string UserName,
    decimal TotalAmount,
    IReadOnlyList<PayoutBreakdownDto> Breakdown,
    bool IsPaid,
    DateTime? PaidAtUtc,
    bool HasDiscrepancy,
    bool HasSharedDetails,
    string? AccountName,
    string? SortCode,
    string? AccountNumber);
