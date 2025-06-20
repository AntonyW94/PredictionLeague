using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;

namespace PredictionLeague.Infrastructure.Services
{
    public class PredictionService : IPredictionService
    {
        private readonly IUserPredictionRepository _predictionRepository;
        private readonly IMatchRepository _matchRepository;

        public PredictionService(IUserPredictionRepository predictionRepository, IMatchRepository matchRepository)
        {
            _predictionRepository = predictionRepository;
            _matchRepository = matchRepository;
        }

        public async Task SubmitPredictionsAsync(string userId, int gameWeekId, IEnumerable<UserPrediction> predictions)
        {
            // In a real system, add validation:
            // 1. Check if the gameweek deadline has passed.
            // 2. Ensure all predictions are for matches within the specified gameWeekId.

            foreach (var prediction in predictions)
            {
                prediction.UserId = userId; // Ensure the user ID is set correctly
                await _predictionRepository.UpsertAsync(prediction);
            }
        }

        public async Task CalculatePointsForMatchAsync(int matchId)
        {
            var match = await _matchRepository.GetByIdAsync(matchId);
            if (match == null || match.Status != MatchStatus.Completed || match.ActualHomeTeamScore == null || match.ActualAwayTeamScore == null)
            {
                throw new InvalidOperationException("Match is not completed or scores are not set.");
            }

            var predictions = await _predictionRepository.GetByMatchIdAsync(matchId);

            foreach (var prediction in predictions)
            {
                prediction.PointsAwarded = CalculatePoints(
                    match.ActualHomeTeamScore.Value,
                    match.ActualAwayTeamScore.Value,
                    prediction.PredictedHomeScore,
                    prediction.PredictedAwayScore);

                // We need to update the prediction record with the points.
                // The current UpsertAsync only handles score submission, so a new method is needed.
                // For now, we will assume an Update method exists in the repository.
                // await _predictionRepository.UpdateAsync(prediction);
            }
            // This highlights a need to enhance IUserPredictionRepository with an Update method.
        }

        private int CalculatePoints(int actualHome, int actualAway, int predictedHome, int predictedAway)
        {
            // Correct score
            if (actualHome == predictedHome && actualAway == predictedAway)
            {
                return 3; // Example: 3 points for exact score
            }

            // Correct result (win/draw/loss)
            var actualResult = Math.Sign(actualHome - actualAway);
            var predictedResult = Math.Sign(predictedHome - predictedAway);
            if (actualResult == predictedResult)
            {
                return 1; // Example: 1 point for correct result
            }

            return 0;
        }
    }
}
