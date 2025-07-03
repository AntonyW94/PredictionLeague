using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Shared.Predictions;

namespace PredictionLeague.Infrastructure.Services
{
    public class PredictionService : IPredictionService
    {
        private readonly IUserPredictionRepository _predictionRepository;
        private readonly IMatchRepository _matchRepository;
        private readonly IRoundRepository _roundRepository;

        public PredictionService(IUserPredictionRepository predictionRepository, IMatchRepository matchRepository, IRoundRepository roundRepository)
        {
            _predictionRepository = predictionRepository;
            _matchRepository = matchRepository;
            _roundRepository = roundRepository;
        }

        public async Task SubmitPredictionsAsync(string userId, SubmitPredictionsRequest request)
        {
            var round = await _roundRepository.GetByIdAsync(request.RoundId);
            if (round == null || round.Deadline < DateTime.UtcNow)
            {
                throw new InvalidOperationException("The prediction deadline has passed for this round.");
            }

            foreach (var predictionDto in request.Predictions)
            {
                var prediction = new UserPrediction
                {
                    MatchId = predictionDto.MatchId,
                    UserId = userId,
                    PredictedHomeScore = predictionDto.PredictedHomeScore,
                    PredictedAwayScore = predictionDto.PredictedAwayScore
                };
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

        private static int CalculatePoints(int actualHome, int actualAway, int predictedHome, int predictedAway)
        {
            // Correct score
            if (actualHome == predictedHome && actualAway == predictedAway)
            {
                return 3; // Example: 3 points for exact score
            }

            // Correct result (win/draw/loss)
            var actualResult = Math.Sign(actualHome - actualAway);
            var predictedResult = Math.Sign(predictedHome - predictedAway);
            return actualResult == predictedResult ? 1 : 0;
        }
    }
}
