using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateRoundCommand : UpdateRoundRequest, IRequest, ITransactionalRequest
{
    public int RoundId { get; }

    public UpdateRoundCommand(int roundId, UpdateRoundRequest request)
    {
        RoundId = roundId;
        StartDate = request.StartDate;
        Deadline = request.Deadline;
        Matches = request.Matches;
    }
}