namespace ThePredictions.Contracts.Leagues;

/// <summary>
/// Implemented by league requests that carry optional peer-to-peer entry-fee bank details,
/// so the same validation rules can be shared across create and update.
/// </summary>
public interface IHasBankDetails
{
    string? BankAccountName { get; set; }
    string? BankSortCode { get; set; }
    string? BankAccountNumber { get; set; }
    string? PaymentReferenceTemplate { get; set; }
}
