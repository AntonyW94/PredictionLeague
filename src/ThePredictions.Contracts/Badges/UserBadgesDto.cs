namespace ThePredictions.Contracts.Badges;

/// <summary>The full badges page: the three sections plus the overall collected count.</summary>
public record UserBadgesDto(
    int EarnedCount,
    int TotalCount,
    IReadOnlyList<BadgeDto> Collections,
    IReadOnlyList<BadgeDto> Badges,
    IReadOnlyList<BadgeDto> Honours);
