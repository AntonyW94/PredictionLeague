namespace PredictionLeague.Contracts.Admin.Users;

public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAdmin,
    bool HasLocalPassword,
    List<string> SocialProviders
);