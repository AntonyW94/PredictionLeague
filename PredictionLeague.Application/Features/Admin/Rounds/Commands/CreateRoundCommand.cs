using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommand : CreateRoundRequest, IRequest, ITransactionalRequest;