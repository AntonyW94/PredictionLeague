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
    Task<IEnumerable<League>> GetLeaguesForScoringAsync(int seasonId, int roundId, CancellationToken cancellationToken);
    Task<IEnumerable<League>> GetLeaguesByAdministratorIdAsync(string administratorId, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(League league, CancellationToken cancellationToken);
    Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus status, CancellationToken cancellationToken);
    Task UpdatePredictionPointsAsync(IEnumerable<UserPrediction> predictionsToUpdate, CancellationToken cancellationToken);

    #endregion
    
    #region Delete

    Task DeleteAsync(int leagueId, CancellationToken cancellationToken);

    #endregion
}