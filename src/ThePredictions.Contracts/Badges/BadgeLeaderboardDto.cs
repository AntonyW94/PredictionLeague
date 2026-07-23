namespace ThePredictions.Contracts.Badges;

/// <summary>The site-wide badges leaderboard: every player ranked by badges collected.</summary>
public record BadgeLeaderboardDto(
    int TotalPlayers,
    int? YourRank,
    IReadOnlyList<BadgeLeaderboardRowDto> Rows);
