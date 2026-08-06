using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Badges;

/// <summary>
/// The dashboard tile: the overall collected count and the ordered carousel window
/// (recently earned first, then closest-to-next).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record BadgesTileDto(
    int EarnedCount,
    int TotalCount,
    IReadOnlyList<BadgeDto> Carousel)
{
    /// <summary>The player's rank on the site-wide badges leaderboard, and how many players there are.</summary>
    public int? YourRank { get; init; }
    public int TotalPlayers { get; init; }
}
