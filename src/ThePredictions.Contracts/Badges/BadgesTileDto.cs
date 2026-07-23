namespace ThePredictions.Contracts.Badges;

/// <summary>
/// The dashboard tile: the overall collected count and the ordered carousel window
/// (recently earned first, then closest-to-next).
/// </summary>
public record BadgesTileDto(
    int EarnedCount,
    int TotalCount,
    IReadOnlyList<BadgeDto> Carousel);
