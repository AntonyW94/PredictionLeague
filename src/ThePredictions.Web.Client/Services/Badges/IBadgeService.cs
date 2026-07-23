using ThePredictions.Contracts.Badges;

namespace ThePredictions.Web.Client.Services.Badges;

public interface IBadgeService
{
    Task<UserBadgesDto?> GetBadgesAsync();
    Task<UserBadgesDto?> GetBadgesForUserAsync(string userId);
    Task<BadgesTileDto?> GetTileAsync();
    Task<BadgeLeaderboardDto?> GetLeaderboardAsync();
}
