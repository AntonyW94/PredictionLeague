using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Application.Services;

public interface IPredictionService
{
    Task SubmitPredictionsAsync(string userId, SubmitPredictionsRequest request);
    Task<PredictionPageDto> GetPredictionPageDataAsync(int roundId, string userId);
}