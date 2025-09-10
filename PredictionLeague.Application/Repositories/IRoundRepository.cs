using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundRepository
{
    #region Create

    Task<Round> CreateAsync(Round round, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IEnumerable<int>> GetMatchIdsWithPredictionsAsync(IEnumerable<int> matchIds);
    Task<bool> IsLastRoundOfMonthAsync(int roundId, int seasonId, CancellationToken cancellationToken);
    Task<bool> IsLastRoundOfSeasonAsync(int roundId, int seasonId, CancellationToken cancellationToken);
    Task<IEnumerable<Match>> GetAllMatchesForMonthAsync(int month, int seasonId, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(Round round, CancellationToken cancellationToken);
    Task UpdateMatchScoresAsync(List<Match> matches, CancellationToken cancellationToken);

    #endregion
}