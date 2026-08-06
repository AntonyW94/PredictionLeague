using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Payouts;

[ExcludeFromCodeCoverage]
public class SetPayoutDetailsRequest
{
    public string? AccountName { get; set; }
    public string? SortCode { get; set; }
    public string? AccountNumber { get; set; }
}
