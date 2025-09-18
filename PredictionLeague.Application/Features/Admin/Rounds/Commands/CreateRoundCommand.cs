using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Matches;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record CreateRoundCommand(
    int SeasonId, 
    int RoundNumber,
    string ApiRoundName,
    DateTime StartDate,
    DateTime Deadline, 
    List<CreateMatchRequest> Matches) : IRequest<RoundDto>, ITransactionalRequest;