namespace ThePredictions.Contracts.Admin.PricingSettings;

/// <summary>
/// Global calculator knobs. BufferRate is a fraction (0.15 = 15%); MinimumFloor is a GBP amount.
/// Provider fees (Stripe, SMS, email) are returned separately as service fees.
/// </summary>
public record PricingSettingsDto(
    decimal BufferRate,
    decimal MinimumFloor);
