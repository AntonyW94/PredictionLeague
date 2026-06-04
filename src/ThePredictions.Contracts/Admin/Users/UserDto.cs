namespace ThePredictions.Contracts.Admin.Users;

public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAdmin,
    bool HasLocalPassword,
    List<string> SocialProviders,
    bool EmailConfirmed,
    bool HasSeasonPass,
    int LeaguesCreated,
    int LeaguesJoinedApproved,
    int LeaguesJoinedPending,
    decimal TotalWinnings,
    decimal SeasonPassSpend,
    decimal LeagueEntrySpend
)
{
    public decimal TotalSpend => SeasonPassSpend + LeagueEntrySpend;

    public bool IsDormant =>
        !HasSeasonPass
        && LeaguesCreated == 0
        && LeaguesJoinedApproved == 0
        && LeaguesJoinedPending == 0;
}
