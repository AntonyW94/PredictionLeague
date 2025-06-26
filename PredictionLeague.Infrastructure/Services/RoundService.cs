using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;

namespace PredictionLeague.Infrastructure.Services;

public class RoundService : IRoundService
{
    private readonly IRoundResultRepository _roundResultRepository;
    private readonly IPredictionService _predictionService;
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly IMatchRepository _matchRepository;

    public RoundService(
        IRoundResultRepository roundResultRepository,
        IPredictionService predictionService,
        IUserPredictionRepository predictionRepository,
        IMatchRepository matchRepository)
    {
        _roundResultRepository = roundResultRepository;
        _predictionService = predictionService;
        _predictionRepository = predictionRepository;
        _matchRepository = matchRepository;
    }

    public async Task UpdateResultsAsync(int roundId, IEnumerable<Match> completedMatches)
    {
        // First, calculate points for each match individually
        foreach (var match in completedMatches)
        {
            if (match.Status == MatchStatus.Completed)
            {
                await _predictionService.CalculatePointsForMatchAsync(match.Id);
            }
        }

        // Now, aggregate the results for the entire round
        var allPredictionsForRound = new List<UserPrediction>();
        var matchesInRound = await _matchRepository.GetByRoundIdAsync(roundId);
        foreach (var match in matchesInRound)
        {
            allPredictionsForRound.AddRange(await _predictionRepository.GetByMatchIdAsync(match.Id));
        }

        var userScores = allPredictionsForRound
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.PointsAwarded ?? 0)
            });

        foreach (var score in userScores)
        {
            var roundResult = new RoundResult
            {
                RoundId = roundId,
                UserId = score.UserId,
                TotalPoints = score.TotalPoints
            };
            await _roundResultRepository.UpsertAsync(roundResult);
        }
    }
}