using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using System.Transactions;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommandHandler : IRequestHandler<UpdateMatchResultsCommand>
{
    private readonly ILogger<UpdateMatchResultsCommandHandler> _logger;
    private readonly IMatchRepository _matchRepository;
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly IRoundResultRepository _roundResultRepository;
    private readonly IPointsCalculationService _pointsService;

    public UpdateMatchResultsCommandHandler(
        ILogger<UpdateMatchResultsCommandHandler> logger,
        IMatchRepository matchRepository,
        IUserPredictionRepository predictionRepository,
        IRoundResultRepository roundResultRepository,
        IPointsCalculationService pointsService)
    {
        _logger = logger;
        _matchRepository = matchRepository;
        _predictionRepository = predictionRepository;
        _roundResultRepository = roundResultRepository;
        _pointsService = pointsService;
    }

    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        if (request.Results == null || !request.Results.Any())
            return;

        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            foreach (var result in request.Results)
            {
                var match = await _matchRepository.GetByIdAsync(result.MatchId);
                if (match == null)
                {
                    _logger.LogWarning("Attempted to update results for a non-existent Match (ID: {MatchId}).", result.MatchId);
                    continue;
                }

                match.ActualHomeTeamScore = result.HomeScore;
                match.ActualAwayTeamScore = result.AwayScore;
                match.Status = result.IsFinal ? MatchStatus.Completed : MatchStatus.InProgress;

                await _matchRepository.UpdateAsync(match);
                await _pointsService.CalculatePointsForMatchAsync(match);
            }
            scope.Complete();
        }

        await AggregateResultsForRoundAsync(request.RoundId);

        var allMatchesInRound = await _matchRepository.GetByRoundIdAsync(request.RoundId);
        if (allMatchesInRound.All(m => m.Status == MatchStatus.Completed))
        {
            // TODO: Calculate Winnings Here
        }
    }

    private async Task AggregateResultsForRoundAsync(int roundId)
    {
        var matchesInRound = await _matchRepository.GetByRoundIdAsync(roundId);
        var allPredictionsForRound = new List<UserPrediction>();

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
            var roundResult = new RoundResult(roundId, score.UserId, score.TotalPoints);
            await _roundResultRepository.UpsertAsync(roundResult);
        }
    }
}