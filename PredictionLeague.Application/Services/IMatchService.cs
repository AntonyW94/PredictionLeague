using PredictionLeague.Shared.Admin.Results;

namespace PredictionLeague.Application.Services;

public interface IMatchService
{
    Task UpdateMatchResultsAsync(List<UpdateMatchResultsRequest>? results);
}