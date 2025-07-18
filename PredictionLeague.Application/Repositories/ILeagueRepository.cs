using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ILeagueRepository
{
    #region Create

    Task CreateAsync(League league);
    Task AddMemberAsync(LeagueMember member);

    #endregion

    #region Read

    Task<League?> GetByIdAsync(int id);
    Task<League?> GetByEntryCodeAsync(string entryCode);
    Task<IEnumerable<League>> GetAllAsync();
    Task<IEnumerable<League>> GetPublicLeaguesAsync();
    Task<IEnumerable<League>> GetLeaguesByUserIdAsync(string userId);
    Task<IEnumerable<LeagueMember>> GetMembersByLeagueIdAsync(int leagueId);

    #endregion

    #region Update

    Task UpdateAsync(League league);
    Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus status);

    #endregion
}