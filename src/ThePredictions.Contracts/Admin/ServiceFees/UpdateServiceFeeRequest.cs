namespace ThePredictions.Contracts.Admin.ServiceFees;

/// <summary>
/// Update for a provider's fee. PercentFee is a fraction (0.015 = 1.5%); FixedFee is a GBP amount.
/// </summary>
public class UpdateServiceFeeRequest
{
    public decimal PercentFee { get; set; }
    public decimal FixedFee { get; set; }
}
