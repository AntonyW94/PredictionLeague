using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

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
    Task<Dictionary<int, Round>> GetPublishedRoundsAsync(CancellationToken cancellationToken);

    #endregion

    #region Update

    Task UpdateAsync(Round round, CancellationToken cancellationToken);
    Task MoveMatchesToRoundAsync(IEnumerable<int> matchIds, int targetRoundId, CancellationToken cancellationToken);
    Task UpdateMatchScoresAsync(List<Match> matches, CancellationToken cancellationToken);
    /// <summary>
    /// Stores each player's tally for a round. Existing rows are updated and new ones added; nothing is removed, so a
    /// player whose predictions have gone back to unjudged keeps the tally they had.
    /// </summary>
    /// <remarks>
    /// The counting is <c>Domain.Services.OutcomeTally</c>. This used to be a <c>MERGE</c> that did both, which meant the
    /// rule lived in a statement nothing could execute without a database.
    /// </remarks>
    Task UpdateRoundResultsAsync(int roundId, IEnumerable<RoundResultTally> tallies, CancellationToken cancellationToken);
    Task UpdateLastReminderSentAsync(Round round, CancellationToken cancellationToken);
    Task UpdateResultsDigestSentAsync(Round round, CancellationToken cancellationToken);

    #endregion
}