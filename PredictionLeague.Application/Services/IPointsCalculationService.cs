using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services;

public interface IPointsCalculationService
{
    Task CalculatePointsForMatchAsync(Match match);
}