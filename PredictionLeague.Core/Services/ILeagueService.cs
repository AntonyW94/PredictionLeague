using PredictionLeague.Core.Models;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.Core.Services;

public interface ILeagueService
{
    Task<League> CreateLeagueAsync(CreateLeagueRequest request, string administratorUserId);
    Task JoinLeagueAsync(string entryCode, string userId);
    Task JoinPublicLeagueAsync(int leagueId, string userId);
    Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId);
    Task<League?> GetDefaultPublicLeagueAsync();
}