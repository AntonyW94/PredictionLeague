using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(int id);
    Task<IEnumerable<League>> GetAllAsync();
    Task<IEnumerable<League>> GetPublicLeaguesAsync();
    Task<IEnumerable<League>> GetLeaguesByUserIdAsync(string userId);
    Task<League?> GetDefaultPublicLeagueAsync();
    Task CreateAsync(League league);
    Task UpdateAsync(League league);
    Task AddMemberAsync(int leagueId, string userId);
    Task<IEnumerable<LeagueMember>> GetMembersByLeagueIdAsync(int leagueId);
}