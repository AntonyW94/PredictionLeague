using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Services;

public interface IRoundService
{
    Task UpdateResultsAsync(int roundId, IEnumerable<Match> completedMatches);
}