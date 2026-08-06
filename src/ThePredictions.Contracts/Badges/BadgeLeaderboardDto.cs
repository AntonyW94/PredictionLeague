using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Badges;

/// <summary>The site-wide badges leaderboard: every player ranked by badges collected.</summary>
[ExcludeFromCodeCoverage]
public record BadgeLeaderboardDto(
    int TotalPlayers,
    int? YourRank,
    IReadOnlyList<BadgeLeaderboardRowDto> Rows);
