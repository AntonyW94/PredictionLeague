namespace PredictionLeague.Contracts.Admin.Users;

public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber, 
    bool IsAdmin,
    string? PasswordHash,
    List<string> SocialProviders
);