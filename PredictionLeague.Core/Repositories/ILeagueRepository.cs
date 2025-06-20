using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface ILeagueRepository
{
    Task CreateAsync(League league);
    Task AddMemberAsync(int leagueId, string userId);
    Task<League?> GetByIdAsync(int id);
    Task<League?> GetByEntryCodeAsync(string entryCode);
    Task<IEnumerable<LeagueMember>> GetMembersByLeagueIdAsync(int leagueId);
}