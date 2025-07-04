using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface IUserPredictionRepository
{
    Task<IEnumerable<UserPrediction>> GetByMatchIdAsync(int matchId);
    Task<IEnumerable<UserPrediction>> GetByUserIdAndRoundIdAsync(string userId, int roundId);
    Task UpsertAsync(UserPrediction prediction);
    Task UpdatePointsAsync(int predictionId, int points);
}