using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record UpdateRoundCommand(
    int RoundId, 
    int RoundNumber, 
    DateTime StartDate, 
    DateTime Deadline, 
    List<UpdateMatchRequest> Matches) : IRequest, ITransactionalRequest;