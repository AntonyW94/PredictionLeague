using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One winner's bank details, as stored ciphertext.
/// </summary>
/// <remarks>
/// Named for the ciphertext so nobody downstream mistakes these for something readable. Whether the three parts add up
/// to an account that can be paid is <c>BankDetails.AreComplete</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PayoutBankDetailsRow(
    string UserId,
    string? EncryptedAccountName,
    string? EncryptedSortCode,
    string? EncryptedAccountNumber);
