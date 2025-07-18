using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class GetRoundByIdQuery : IRequest<RoundDetailsDto?>
{
    public int RoundId { get; }

    public GetRoundByIdQuery(int roundId)
    {
        RoundId = roundId;
    }
}