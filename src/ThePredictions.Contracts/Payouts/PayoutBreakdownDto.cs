using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Payouts;

/// <summary>One line of a winner's payout breakdown (e.g. Round / Monthly / Overall), computed live from Winnings.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record PayoutBreakdownDto(string PrizeType, decimal Amount);
