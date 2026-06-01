namespace ThePredictions.Contracts.Admin.ServiceFees;

/// <summary>
/// A per-transaction fee charged by a provider. <see cref="Provider"/> is the enum name
/// (Stripe, Sms, Email). <see cref="PercentFee"/> is a fraction (0.015 = 1.5%); <see cref="FixedFee"/>
/// is a GBP amount per transaction/message.
/// </summary>
public record ServiceFeeDto(
    string Provider,
    decimal PercentFee,
    decimal FixedFee);
