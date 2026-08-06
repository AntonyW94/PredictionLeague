using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.ServiceFees;

/// <summary>
/// Update for a provider's fee. PercentFee is a fraction (0.015 = 1.5%); FixedFee is a GBP amount.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateServiceFeeRequest
{
    public decimal PercentFee { get; set; }
    public decimal FixedFee { get; set; }
}
