using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Badges;

/// <summary>The full badges page: the three sections plus the overall collected count.</summary>
[ExcludeFromCodeCoverage]
public record UserBadgesDto(
    int EarnedCount,
    int TotalCount,
    IReadOnlyList<BadgeDto> Collections,
    IReadOnlyList<BadgeDto> Badges,
    IReadOnlyList<BadgeDto> Honours)
{
    /// <summary>Display name (first name + surname initial) of whose page this is; used when viewing another player.</summary>
    public string OwnerName { get; init; } = string.Empty;
}
