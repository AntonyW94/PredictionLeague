namespace ThePredictions.Contracts.Payouts;

/// <summary>One line of a winner's payout breakdown (e.g. Round / Monthly / Overall), computed live from Winnings.</summary>
public record PayoutBreakdownDto(string PrizeType, decimal Amount);
