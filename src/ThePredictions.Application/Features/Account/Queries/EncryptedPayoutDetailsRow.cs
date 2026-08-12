using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>A player's bank details as stored, which is to say encrypted.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record EncryptedPayoutDetailsRow(
    string? EncryptedAccountName,
    string? EncryptedSortCode,
    string? EncryptedAccountNumber);
