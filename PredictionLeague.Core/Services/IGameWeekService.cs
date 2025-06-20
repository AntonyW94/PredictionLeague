using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Services;

public interface IGameWeekService
{
    Task UpdateResultsAsync(int gameWeekId, IEnumerable<Match> completedMatches);
}