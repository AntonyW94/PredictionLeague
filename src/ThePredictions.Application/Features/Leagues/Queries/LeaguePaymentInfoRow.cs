using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's payment details as stored, with the caller's standing in that league.
/// </summary>
/// <remarks>
/// <see cref="MembershipStatus"/> is the caller's standing in the league, or null where they have none - the status
/// itself rather than a verdict on it, because which standings may see a league's bank account is a rule. It used to
/// arrive as an <c>EXISTS</c> with no status filter, so being turned away left the details readable.
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
    LeagueMemberStatus? MembershipStatus,
    string? RequestingFirstName,
    string? RequestingLastName);
