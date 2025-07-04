using PredictionLeague.Core.Models;
using PredictionLeague.Shared.Dashboard;

namespace PredictionLeague.Core.Services;

public interface ILeagueService
{
    Task<League> CreateLeagueAsync(string name, int seasonId, string administratorUserId);
    Task JoinLeagueAsync(string entryCode, string userId);
    Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId);
    Task JoinPublicLeagueAsync(int leagueId, string userId);
    Task<League?> GetDefaultPublicLeagueAsync();
    Task<IEnumerable<PublicLeagueDto>> GetAllPublicLeaguesForUserAsync(string userId);
}