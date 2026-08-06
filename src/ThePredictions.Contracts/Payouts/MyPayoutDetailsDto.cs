using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Payouts;

/// <summary>
/// A player's own payout details (decrypted for them) plus the disclosure of which league admins can see them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record MyPayoutDetailsDto(
    string? AccountName,
    string? SortCode,
    string? AccountNumber,
    bool HasDetails,
    IReadOnlyList<string> VisibleToAdmins);
