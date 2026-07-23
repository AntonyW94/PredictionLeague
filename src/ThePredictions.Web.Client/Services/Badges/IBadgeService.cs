using ThePredictions.Contracts.Badges;

namespace ThePredictions.Web.Client.Services.Badges;

public interface IBadgeService
{
    Task<UserBadgesDto?> GetBadgesAsync();
    Task<BadgesTileDto?> GetTileAsync();
}
