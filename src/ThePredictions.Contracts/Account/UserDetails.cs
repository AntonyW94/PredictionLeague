using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Account;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UserDetails(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string PreferredTheme,
    bool MarketingOptIn
);
