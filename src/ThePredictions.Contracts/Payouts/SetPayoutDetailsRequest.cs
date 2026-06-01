namespace ThePredictions.Contracts.Payouts;

public class SetPayoutDetailsRequest
{
    public string? AccountName { get; set; }
    public string? SortCode { get; set; }
    public string? AccountNumber { get; set; }
}
