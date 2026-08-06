using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

/// <summary>
/// Peer-to-peer entry-fee payment details surfaced to a league's admin and members.
/// When the admin has set no bank details, <see cref="HasBankDetails"/> is false and the
/// UI falls back to arranging payment manually.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeaguePaymentInfoDto(
    string LeagueName,
    bool HasBankDetails,
    string? AccountName,
    string? SortCode,
    string? AccountNumber,
    decimal Amount,
    string Reference);
