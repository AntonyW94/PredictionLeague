using PredictionLeague.Contracts.Boosts;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ILeagueRepository
{
    #region Create

    Task<League> CreateAsync(League league, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<League?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<League?> GetByEntryCodeAsync(string entryCode, CancellationToken cancellationToken);
    Task<League?> GetByIdWithAllDataAsync(int id, CancellationToken cancellationToken);
    Task<IEnumerable<League>> GetLeaguesByAdministratorIdAsync(string administratorId, CancellationToken cancellationToken);
    Task<IEnumerable<LeagueRoundResult>> GetLeagueRoundResultsAsync(int roundId, CancellationToken cancellationToken);
    Task<IEnumerable<int>> GetLeagueIdsForSeasonAsync(int seasonId, CancellationToken cancellationToken);
    
    #endregion

    #region Update

    Task UpdateAsync(League league, CancellationToken cancellationToken);
    Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus status, CancellationToken cancellationToken);
    Task UpdateLeagueRoundResultsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateLeagueRoundBoostsAsync(IEnumerable<LeagueRoundBoostUpdate> updates, CancellationToken cancellationToken);

    #endregion

    #region Delete

    Task DeleteAsync(int leagueId, CancellationToken cancellationToken);

    #endregion
}