using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IUserPredictionRepository
{
    #region Create
    
    Task UpsertBatchAsync(IEnumerable<UserPrediction> predictions, CancellationToken cancellationToken);
    
    #endregion

    #region Read

    Task<IEnumerable<UserPrediction>> GetByUserIdAndRoundIdAsync(string userId, int roundId);
    Task<IEnumerable<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId);
    Task<IEnumerable<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month);
    Task<IEnumerable<LeaderboardEntryDto>> GetRoundLeaderboardAsync(int leagueId, int roundId);
 
    #endregion
}