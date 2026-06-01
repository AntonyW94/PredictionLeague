namespace ThePredictions.Contracts.Admin.PricingSettings;

/// <summary>
/// Update for the global calculator knobs. BufferRate is a fraction (0.15 = 15%);
/// MinimumFloor is a GBP amount.
/// </summary>
public class UpdatePricingSettingsRequest
{
    public decimal BufferRate { get; set; }
    public decimal MinimumFloor { get; set; }
}
