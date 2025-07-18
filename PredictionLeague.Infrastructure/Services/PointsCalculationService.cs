using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Domain.Services;

namespace PredictionLeague.Infrastructure.Services;

/// <summary>
/// An Application Service responsible for the logic of calculating points for predictions.
/// </summary>
public class PointsCalculationService : IPointsCalculationService
{
    private readonly IUserPredictionRepository _predictionRepository;

    public PointsCalculationService(IUserPredictionRepository predictionRepository)
    {
        _predictionRepository = predictionRepository;
    }

    public async Task CalculatePointsForMatchAsync(Match match)
    {
        if (match.ActualHomeTeamScore == null || match.ActualAwayTeamScore == null)
        {
            return;
        }

        var predictions = await _predictionRepository.GetByMatchIdAsync(match.Id);

        foreach (var prediction in predictions)
        {
            var points = Scoring.CalculatePoints(
                match.ActualHomeTeamScore.Value,
                match.ActualAwayTeamScore.Value,
                prediction.PredictedHomeScore,
                prediction.PredictedAwayScore);

            await _predictionRepository.UpdatePointsAsync(prediction.Id, points);
        }
    }
}