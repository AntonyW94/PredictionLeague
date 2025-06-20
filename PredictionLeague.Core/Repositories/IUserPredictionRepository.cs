using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface IUserPredictionRepository
{
    Task<IEnumerable<UserPrediction>> GetByMatchIdAsync(int matchId);
    Task<IEnumerable<UserPrediction>> GetByUserIdAndGameWeekIdAsync(string userId, int gameWeekId);
    Task UpsertAsync(UserPrediction prediction); // Insert or Update
}