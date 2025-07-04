using PredictionLeague.Shared.Predictions;

namespace PredictionLeague.Core.Services;

public interface IPredictionService
{
    Task SubmitPredictionsAsync(string userId, SubmitPredictionsRequest request);
    Task CalculatePointsForMatchAsync(int matchId);
    Task<PredictionPageDto> GetPredictionPageDataAsync(int roundId, string userId);
    Task CalculatePointsForRoundAsync(int roundId);
}