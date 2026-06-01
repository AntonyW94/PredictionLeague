namespace ThePredictions.Contracts.Leagues;

/// <summary>
/// Decrypted league bank details returned to the league administrator only, to pre-fill the edit form.
/// </summary>
public record LeagueBankDetailsDto(
    string? BankAccountName,
    string? BankSortCode,
    string? BankAccountNumber,
    string? PaymentReferenceTemplate);
