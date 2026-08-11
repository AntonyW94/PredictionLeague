using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's payment details as stored, with the caller's standing in that league.
/// </summary>
/// <remarks>
/// <see cref="HasMembership"/> is true for <b>any</b> membership row, whatever its status - approved, pending or
/// rejected. That is faithful to the old <c>EXISTS</c>, which had no status filter, and it is load-bearing for the
/// pending case: someone who has asked to join needs the bank details in order to pay. Whether it should also cover
/// somebody who was turned away is a question, recorded in the plan document.
///
/// The three bank fields are the stored ciphertext. They are named for it so that nobody downstream mistakes them for
/// something readable.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaguePaymentInfoRow(
    string LeagueName,
    decimal Price,
    string? EntryCode,
    string? EncryptedAccountName,
    string? EncryptedSortCode,
    string? EncryptedAccountNumber,
    string? PaymentReferenceTemplate,
    bool IsAdministrator,
    bool HasMembership,
    string? RequestingFirstName,
    string? RequestingLastName);
