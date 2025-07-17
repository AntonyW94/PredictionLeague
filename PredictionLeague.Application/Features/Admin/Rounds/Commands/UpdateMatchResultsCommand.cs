using MediatR;
using PredictionLeague.Contracts.Admin.Results;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommand : IRequest
{
    public int RoundId { get; }
    public List<UpdateMatchResultsRequest>? Results { get; }

    public UpdateMatchResultsCommand(int roundId, List<UpdateMatchResultsRequest>? results)
    {
        RoundId = roundId;
        Results = results;
    }
}