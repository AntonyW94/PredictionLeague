using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;

namespace PredictionLeague.Infrastructure.Services;

public class GameWeekService : IGameWeekService
{
    private readonly IGameWeekResultRepository _gameWeekResultRepository;
    private readonly IPredictionService _predictionService;
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly IMatchRepository _matchRepository;

    public GameWeekService(
        IGameWeekResultRepository gameWeekResultRepository,
        IPredictionService predictionService,
        IUserPredictionRepository predictionRepository,
        IMatchRepository matchRepository)
    {
        _gameWeekResultRepository = gameWeekResultRepository;
        _predictionService = predictionService;
        _predictionRepository = predictionRepository;
        _matchRepository = matchRepository;
    }

    public async Task UpdateResultsAsync(int gameWeekId, IEnumerable<Match> completedMatches)
    {
        // First, calculate points for each match individually
        foreach (var match in completedMatches)
        {
            if (match.Status == MatchStatus.Completed)
            {
                await _predictionService.CalculatePointsForMatchAsync(match.Id);
            }
        }

        // Now, aggregate the results for the entire gameweek
        var allPredictionsForWeek = new List<UserPrediction>();
        var matchesInWeek = await _matchRepository.GetByGameWeekIdAsync(gameWeekId);
        foreach (var match in matchesInWeek)
        {
            allPredictionsForWeek.AddRange(await _predictionRepository.GetByMatchIdAsync(match.Id));
        }

        var userScores = allPredictionsForWeek
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.PointsAwarded ?? 0)
            });

        foreach (var score in userScores)
        {
            var gameWeekResult = new GameWeekResult
            {
                GameWeekId = gameWeekId,
                UserId = score.UserId,
                TotalPoints = score.TotalPoints
            };
            await _gameWeekResultRepository.UpsertAsync(gameWeekResult);
        }
    }
}