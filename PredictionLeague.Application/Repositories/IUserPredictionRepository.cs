using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IUserPredictionRepository
{
    #region Create
    
    Task UpsertBatchAsync(IEnumerable<UserPrediction> predictions, CancellationToken cancellationToken);
    
    #endregion

    #region Read

    Task<IEnumerable<UserPrediction>> FetchByUserIdAndRoundIdAsync(string userId, int roundId, CancellationToken cancellationToken);
    Task<IEnumerable<LeaderboardEntryDto>> FetchOverallLeaderboardAsync(int leagueId, CancellationToken cancellationToken);
    Task<IEnumerable<LeaderboardEntryDto>> FetchMonthlyLeaderboardAsync(int leagueId, int month, CancellationToken cancellationToken);
    Task<IEnumerable<LeaderboardEntryDto>> FetchRoundLeaderboardAsync(int leagueId, int roundId, CancellationToken cancellationToken);
 
    #endregion
}