namespace ThePredictions.Contracts.Badges;

/// <summary>One row of the site-wide badges leaderboard. Names are first name + surname initial only.</summary>
public record BadgeLeaderboardRowDto(
    int Rank,
    string UserId,
    string DisplayName,
    int BadgeCount,
    int TotalBadges,
    DateTime? LastAwardedUtc,
    bool IsCurrentUser);
