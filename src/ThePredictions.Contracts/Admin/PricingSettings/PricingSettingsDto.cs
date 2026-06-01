namespace ThePredictions.Contracts.Admin.PricingSettings;

/// <summary>
/// Admin-editable inputs to the recommended-price calculator. Rates are fractions
/// (0.15 = 15%, 0.015 = 1.5%); the fixed fee and floor are GBP amounts.
/// </summary>
public record PricingSettingsDto(
    decimal BufferRate,
    decimal StripePercent,
    decimal StripeFixedFee,
    decimal MinimumFloor);
