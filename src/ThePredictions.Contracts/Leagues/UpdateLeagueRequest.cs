namespace ThePredictions.Contracts.Leagues;

public class UpdateLeagueRequest : IHasBankDetails
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime EntryDeadlineUtc { get; set; }
    public int PointsForExactScore { get; set; }
    public int PointsForCorrectResult { get; set; }

    // Optional peer-to-peer entry-fee bank details (plaintext in transit; encrypted server-side at rest).
    public string? BankAccountName { get; set; }
    public string? BankSortCode { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? PaymentReferenceTemplate { get; set; }
}