using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Matches;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record UpdateRoundCommand(
    int RoundId, 
    int RoundNumber, 
    DateTime StartDate, 
    DateTime Deadline, 
    RoundStatus Status,
    List<UpdateMatchRequest> Matches) : IRequest, ITransactionalRequest;