using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Dashboard;
using PredictionLeague.Shared.Predictions;

namespace PredictionLeague.Infrastructure.Services;

public class PredictionService : IPredictionService
{
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ISeasonRepository _seasonRepository;

    public PredictionService(IUserPredictionRepository predictionRepository, IMatchRepository matchRepository, IRoundRepository roundRepository, ISeasonRepository seasonRepository)
    {
        _predictionRepository = predictionRepository;
        _matchRepository = matchRepository;
        _roundRepository = roundRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task SubmitPredictionsAsync(string userId, SubmitPredictionsRequest request)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId);
        if (round == null || round.Deadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("The prediction deadline has passed for this round.");
        }

        foreach (var prediction in request.Predictions.Select(predictionDto => new UserPrediction { MatchId = predictionDto.MatchId, UserId = userId, PredictedHomeScore = predictionDto.PredictedHomeScore, PredictedAwayScore = predictionDto.PredictedAwayScore }))
        {
            await _predictionRepository.UpsertAsync(prediction);
        }
    }

    public async Task CalculatePointsForMatchAsync(int matchId)
    {
        var match = await _matchRepository.GetByIdAsync(matchId);
        if (match is not { Status: MatchStatus.Completed } || match.ActualHomeTeamScore == null || match.ActualAwayTeamScore == null)
            throw new InvalidOperationException("Match is not completed or scores are not set.");

        var predictions = await _predictionRepository.GetByMatchIdAsync(matchId);

        foreach (var prediction in predictions)
        {
            var points = CalculatePoints(
                match.ActualHomeTeamScore.Value,
                match.ActualAwayTeamScore.Value,
                prediction.PredictedHomeScore,
                prediction.PredictedAwayScore);

            await _predictionRepository.UpdatePointsAsync(prediction.Id, points);
        }
    }

    private static int CalculatePoints(int actualHome, int actualAway, int predictedHome, int predictedAway)
    {
        if (actualHome == predictedHome && actualAway == predictedAway)
            return 5;

        var actualResult = Math.Sign(actualHome - actualAway);
        var predictedResult = Math.Sign(predictedHome - predictedAway);
        return actualResult == predictedResult ? 3 : 0;
    }

    public async Task<PredictionPageDto> GetPredictionPageDataAsync(int roundId, string userId)
    {
        var round = await _roundRepository.GetByIdAsync(roundId) ?? throw new KeyNotFoundException("Round not found.");
        var season = await _seasonRepository.GetByIdAsync(round.SeasonId) ?? throw new KeyNotFoundException("Season not found.");
        var matches = await _matchRepository.GetByRoundIdAsync(roundId);
        var userPredictions = await _predictionRepository.GetByUserIdAndRoundIdAsync(userId, roundId);

        var pageData = new PredictionPageDto
        {
            RoundId = round.Id,
            RoundNumber = round.RoundNumber,
            SeasonName = season.Name,
            Deadline = round.Deadline,
            IsPastDeadline = round.Deadline < DateTime.UtcNow,
            Matches = matches.Select(m =>
            {
                var prediction = userPredictions.FirstOrDefault(p => p.MatchId == m.Id);
                return new MatchPredictionDto
                {
                    MatchId = m.Id,
                    MatchDateTime = m.MatchDateTime,
                    HomeTeamName = m.HomeTeam!.Name,
                    HomeTeamLogoUrl = m.HomeTeam.LogoUrl!,
                    AwayTeamName = m.AwayTeam!.Name,
                    AwayTeamLogoUrl = m.AwayTeam.LogoUrl!,
                    PredictedHomeScore = prediction?.PredictedHomeScore,
                    PredictedAwayScore = prediction?.PredictedAwayScore
                };
            }).ToList()
        };

        return pageData;
    }

    public async Task CalculatePointsForRoundAsync(int roundId)
    {
        var matchesInRound = await _matchRepository.GetByRoundIdAsync(roundId);

        foreach (var match in matchesInRound)
        {
            if (match.Status != MatchStatus.Completed || match.ActualHomeTeamScore == null || match.ActualAwayTeamScore == null)
                continue;

            var predictions = await _predictionRepository.GetByMatchIdAsync(match.Id);

            foreach (var prediction in predictions)
            {
                var points = CalculatePoints(
                    match.ActualHomeTeamScore.Value,
                    match.ActualAwayTeamScore.Value,
                    prediction.PredictedHomeScore,
                    prediction.PredictedAwayScore);

                await _predictionRepository.UpdatePointsAsync(prediction.Id, points);
            }
        }
    }
}