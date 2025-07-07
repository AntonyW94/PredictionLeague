using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Results;
using System.Transactions;

namespace PredictionLeague.Infrastructure.Services
{
    public class MatchService : IMatchService
    {
        public Task UpdateMatchResultsAsync(List<UpdateMatchResultsRequest>? results)
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
                    match.Status = MatchStatus.Completed;

                    await _matchRepository.UpdateAsync(match);
                }
                scope.Complete();
            }

            var firstMatch = await _matchRepository.GetByIdAsync(results.First().MatchId);
            if (firstMatch != null)
                await _predictionService.CalculatePointsForRoundAsync(firstMatch.RoundId);
        }
    }
}
