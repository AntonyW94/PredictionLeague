using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services;

public interface IRoundService
{
    Task UpdateResultsAsync(int roundId, IEnumerable<Match> completedMatches);
}