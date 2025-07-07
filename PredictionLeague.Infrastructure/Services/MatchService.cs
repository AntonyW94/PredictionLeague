using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Results;
using System.Transactions;

namespace PredictionLeague.Infrastructure.Services
{
    public class MatchService : IMatchService
    {
        private readonly IMatchRepository _matchRepository;
        private readonly IPredictionService _predictionService;

        public MatchService(IMatchRepository matchRepository, IPredictionService predictionService)
        {
            _matchRepository = matchRepository;
            _predictionService = predictionService;
        }

        public async Task UpdateMatchResultsAsync(int roundId, List<UpdateMatchResultsRequest>? results)
        {
            if (results == null || !results.Any())
                return;

            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var result in results)
                {
                    var match = await _matchRepository.GetByIdAsync(result.MatchId);
                    if (match == null)
                        continue;

                    match.ActualHomeTeamScore = result.HomeScore;
                    match.ActualAwayTeamScore = result.AwayScore;
                    match.Status = match.MatchDateTime < DateTime.Now ? MatchStatus.Completed : MatchStatus.Scheduled;

                    await _matchRepository.UpdateAsync(match);
                }
                scope.Complete();
            }

            await _predictionService.CalculatePointsForRoundAsync(roundId);
        }
    }
}
