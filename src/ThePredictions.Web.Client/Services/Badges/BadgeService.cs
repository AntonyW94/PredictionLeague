using System.Net.Http.Json;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Web.Client.Services.Badges;

public class BadgeService(HttpClient httpClient) : IBadgeService
{
    public async Task<UserBadgesDto?> GetBadgesAsync()
    {
        return await httpClient.GetFromJsonAsync<UserBadgesDto>("api/badges");
    }

    public async Task<BadgesTileDto?> GetTileAsync()
    {
        return await httpClient.GetFromJsonAsync<BadgesTileDto>("api/badges/tile");
    }
}
