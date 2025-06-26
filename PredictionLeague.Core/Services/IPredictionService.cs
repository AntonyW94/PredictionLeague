using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Services
{
    public interface IPredictionService
    {
        Task SubmitPredictionsAsync(string userId, int roundId, IEnumerable<UserPrediction> predictions);
        Task CalculatePointsForMatchAsync(int matchId);
    }
}
