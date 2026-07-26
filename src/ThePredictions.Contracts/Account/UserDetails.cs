namespace ThePredictions.Contracts.Account;

public record UserDetails(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string PreferredTheme,
    bool MarketingOptIn
);