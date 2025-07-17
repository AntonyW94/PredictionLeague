using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Transactions;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommandHandler : IRequestHandler<UpdateMatchResultsCommand>
{
    private readonly IMatchRepository _matchRepository;
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly IRoundResultRepository _roundResultRepository;

    public UpdateMatchResultsCommandHandler(
        IMatchRepository matchRepository,
        IUserPredictionRepository predictionRepository,
        IRoundResultRepository roundResultRepository)
    {
        _matchRepository = matchRepository;
        _predictionRepository = predictionRepository;
        _roundResultRepository = roundResultRepository;
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
                    continue;

                match.ActualHomeTeamScore = result.HomeScore;
                match.ActualAwayTeamScore = result.AwayScore;

                if (result.IsFinal)
                    match.Status = MatchStatus.Completed;
                else
                    match.Status = MatchStatus.InProgress;

                await _matchRepository.UpdateAsync(match);
                await CalculatePointsForMatchAsync(match);
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

    private async Task CalculatePointsForMatchAsync(Match match)
    {
        var predictions = await _predictionRepository.GetByMatchIdAsync(match.Id);

        foreach (var prediction in predictions)
        {
            var points = CalculatePoints(
                match.ActualHomeTeamScore!.Value,
                match.ActualAwayTeamScore!.Value,
                prediction.PredictedHomeScore,
                prediction.PredictedAwayScore);

            await _predictionRepository.UpdatePointsAsync(prediction.Id, points);
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
            var roundResult = new RoundResult
            {
                RoundId = roundId,
                UserId = score.UserId,
                TotalPoints = score.TotalPoints
            };
            await _roundResultRepository.UpsertAsync(roundResult);
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
}