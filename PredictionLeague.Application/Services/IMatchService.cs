using PredictionLeague.Shared.Admin.Results;

namespace PredictionLeague.Application.Services;

public interface IMatchService
{
    Task UpdateMatchResultsAsync(int roundId, List<UpdateMatchResultsRequest>? results);
}