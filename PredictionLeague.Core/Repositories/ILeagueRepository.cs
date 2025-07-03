using PredictionLeague.Core.Models;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Dashboard;

namespace PredictionLeague.Core.Repositories;

public interface ILeagueRepository
{
    Task CreateAsync(League league);
    Task AddMemberAsync(int leagueId, string userId);
    Task<League?> GetByIdAsync(int id);
    Task<League?> GetByEntryCodeAsync(string entryCode);
    Task<IEnumerable<LeagueMember>> GetMembersByLeagueIdAsync(int leagueId);
    Task<IEnumerable<League>> GetLeaguesByUserIdAsync(string userId);
    Task<League?> GetDefaultPublicLeagueAsync();
    Task<IEnumerable<PublicLeagueDto>> GetJoinablePublicLeaguesAsync(string userId);
    Task<IEnumerable<LeagueDto>> GetAllAsync();
    Task UpdateAsync(League league);
}