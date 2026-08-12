using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>A league's bank details as stored, which is to say encrypted, and who administers it.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record EncryptedLeagueBankDetailsRow(
    string AdministratorUserId,
    string? EncryptedAccountName,
    string? EncryptedSortCode,
    string? EncryptedAccountNumber,
    string? PaymentReferenceTemplate);
