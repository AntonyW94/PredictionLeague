using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Web.Client.Services.Badges;

[ExcludeFromCodeCoverage(Justification = "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply.")]
public class BadgeService(HttpClient httpClient) : IBadgeService
{
    public async Task<UserBadgesDto?> GetBadgesAsync()
    {
        return await httpClient.GetFromJsonAsync<UserBadgesDto>("api/badges");
    }

    public async Task<UserBadgesDto?> GetBadgesForUserAsync(string userId)
    {
        return await httpClient.GetFromJsonAsync<UserBadgesDto>($"api/badges/user/{userId}");
    }

    public async Task<BadgesTileDto?> GetTileAsync()
    {
        return await httpClient.GetFromJsonAsync<BadgesTileDto>("api/badges/tile");
    }

    public async Task<BadgeLeaderboardDto?> GetLeaderboardAsync()
    {
        return await httpClient.GetFromJsonAsync<BadgeLeaderboardDto>("api/badges/leaderboard");
    }
}
