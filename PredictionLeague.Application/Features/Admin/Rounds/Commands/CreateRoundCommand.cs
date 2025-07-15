using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommand : CreateRoundRequest, IRequest;