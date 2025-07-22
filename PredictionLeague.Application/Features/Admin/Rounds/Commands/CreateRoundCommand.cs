using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record CreateRoundCommand(
    int SeasonId, 
    int RoundNumber,
    DateTime StartDate,
    DateTime Deadline, 
    List<CreateMatchRequest> Matches) : IRequest, ITransactionalRequest;