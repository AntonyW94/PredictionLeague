namespace ThePredictions.Contracts.Admin.PricingSettings;

/// <summary>
/// Update for the recommended-price calculator inputs. Rates are fractions
/// (0.15 = 15%, 0.015 = 1.5%); the fixed fee and floor are GBP amounts.
/// </summary>
public class UpdatePricingSettingsRequest
{
    public decimal BufferRate { get; set; }
    public decimal StripePercent { get; set; }
    public decimal StripeFixedFee { get; set; }
    public decimal MinimumFloor { get; set; }
}
