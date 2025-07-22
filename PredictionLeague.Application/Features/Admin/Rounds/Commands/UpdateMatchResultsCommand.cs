using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Results;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record UpdateMatchResultsCommand(
    int RoundId,
    List<MatchResultDto> Matches) : IRequest, ITransactionalRequest;