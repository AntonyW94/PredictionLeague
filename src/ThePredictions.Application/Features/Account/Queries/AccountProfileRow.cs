using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>One player's own account details.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AccountProfileRow(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string PreferredTheme,
    DateTime? MarketingOptInAtUtc);
