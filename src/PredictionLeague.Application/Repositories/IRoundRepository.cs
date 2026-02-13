using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundRepository
{
    #region Create

    Task<Round> CreateAsync(Round round, CancellationToken cancellationToken);

    #endregion

    #region Read

    Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Dictionary<int, Round>> GetAllForSeasonAsync(int seasonId, CancellationToken cancellationToken);
    Task<Round?> GetByApiRoundNameAsync(int seasonId, string apiRoundName, CancellationToken cancellationToken);
    Task<Round?> GetOldestInProgressRoundAsync(int seasonId, CancellationToken cancellationToken);
    Task<IEnumerable<int>> GetMatchIdsWithPredictionsAsync(IEnumerable<int> matchIds, CancellationToken cancellationToken);
    Task<bool> IsLastRoundOfMonthAsync(int roundId, int seasonId, CancellationToken cancellationToken);
    Task<bool> IsLastRoundOfSeasonAsync(int roundId, int seasonId, CancellationToken cancellationToken);
    Task<IEnumerable<int>> GetRoundsIdsForMonthAsync(int month, int seasonId, CancellationToken cancellationToken);
    Task<Round?> GetNextRoundForReminderAsync(CancellationToken cancellationToken);
    Task<Dictionary<int, Round>> GetDraftRoundsStartingBeforeAsync(DateTime dateLimitUtc, CancellationToken cancellationToken);
    Task<Dictionary<int, Round>> GetPublishedRoundsStartingAfterAsync(DateTime dateLimitUtc, CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(Round round, CancellationToken cancellationToken);
    Task MoveMatchesToRoundAsync(IEnumerable<int> matchIds, int targetRoundId, CancellationToken cancellationToken);
    Task UpdateMatchScoresAsync(List<Match> matches, CancellationToken cancellationToken);
    Task UpdateRoundResultsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateLastReminderSentAsync(Round round, CancellationToken cancellationToken);

    #endregion
}