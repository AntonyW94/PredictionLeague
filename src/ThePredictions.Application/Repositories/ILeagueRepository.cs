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

    /// <summary>
    /// Every (league, player) pair that has a tally for this round, with the points the league pays for each kind of
    /// result.
    /// </summary>
    /// <remarks>
    /// Choosing the pairs is fetching: every league running the round's season, and only members it has approved. Turning
    /// a pair into points is <c>Domain.Services.LeagueScoring</c>, which is why the counts and the league's settings come
    /// back rather than a total.
    /// </remarks>
    Task<IEnumerable<LeagueRoundScoringInput>> GetLeagueRoundScoringInputsAsync(
        int roundId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Leagues whose entry deadline has passed, that have a prize scheme, but whose prizes have not
    /// yet been frozen into <see cref="LeaguePrizeSetting"/> rows.
    /// </summary>
    Task<IEnumerable<int>> GetLeagueIdsDueForPrizeFreezeAsync(DateTime nowUtc, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(League league, CancellationToken cancellationToken);
    /// <summary>
    /// Stores each player's points for a round in each of their leagues. Existing rows are updated and new ones added;
    /// nothing is removed.
    /// </summary>
    Task UpdateLeagueRoundResultsAsync(
        int roundId,
        IEnumerable<LeagueRoundScore> scores,
        CancellationToken cancellationToken);
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