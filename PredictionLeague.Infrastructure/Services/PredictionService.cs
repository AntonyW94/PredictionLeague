using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Predictions;
using PredictionLeague.Domain.Models;

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
}