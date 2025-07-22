namespace PredictionLeague.Contracts.Account;

public record UserDetails(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber
);