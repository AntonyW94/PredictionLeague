using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Account;

[ExcludeFromCodeCoverage]
public record UserDetails(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string PreferredTheme,
    bool MarketingOptIn
);
