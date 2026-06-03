using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

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
    Task UpdateLeagueRoundResultsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateLeagueRoundBoostsAsync(IEnumerable<LeagueRoundBoostUpdate> updates, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the league's prize scheme (delete then insert). The write-once rule is enforced in
    /// the domain/handler; this just persists. Kept separate from <see cref="UpdateAsync"/> so that
    /// ordinary league edits (and joins, admin re-assignment) never disturb the scheme.
    /// </summary>
    Task SavePrizeSchemeAsync(int leagueId, LeaguePrizeScheme scheme, CancellationToken cancellationToken);

    #endregion

    #region Delete

    Task DeleteAsync(int leagueId, CancellationToken cancellationToken);

    #endregion
}