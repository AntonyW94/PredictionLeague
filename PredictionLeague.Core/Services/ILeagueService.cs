using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Services;

public interface ILeagueService
{
    Task<League> CreateLeagueAsync(string name, int gameYearId, string administratorUserId);
    Task JoinLeagueAsync(string entryCode, string userId);
    Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId);
}