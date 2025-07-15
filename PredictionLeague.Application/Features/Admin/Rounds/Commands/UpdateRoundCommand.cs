using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands
{
    public class UpdateRoundCommand : UpdateRoundRequest, IRequest
    {
        public int RoundId { get; set; }
    }
}
